using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MonaServer2.Core.Update;

public class BinaryUpdateService
{
    private readonly HttpClient _http;
    private readonly UpdateSettings _settings;
    private readonly ILogger<BinaryUpdateService> _logger;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public event EventHandler<UpdateProgress>? ProgressChanged;

    public BinaryUpdateService(HttpClient http, IOptions<UpdateSettings> settings, ILogger<BinaryUpdateService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public string InstalledVersion => ReadVersionFile();

    public string ResolveInstallDirectory() =>
        !string.IsNullOrWhiteSpace(_settings.InstallDirectory)
            ? _settings.InstallDirectory
            : Path.Combine(AppContext.BaseDirectory, "tools", "monaserver2");

    private string VersionFilePath => Path.Combine(ResolveInstallDirectory(), "VERSION");

    private string ReadVersionFile()
    {
        if (!File.Exists(VersionFilePath)) return "unknown";
        var v = File.ReadAllText(VersionFilePath).Trim();
        return string.IsNullOrEmpty(v) || v == "none" ? "unknown" : v;
    }

    private void WriteVersionFile(string version) =>
        File.WriteAllText(VersionFilePath, version);

    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var installed = InstalledVersion;

        try
        {
            var release = await FetchLatestReleaseAsync(ct);
            if (release is null)
                return new UpdateInfo { InstalledVersion = installed };

            var asset = SelectAsset(release.Assets);
            var latest = release.TagName;
            var latestStripped = latest?.TrimStart('v');
            var installedStripped = installed.TrimStart('v');

            var updateAvailable = latestStripped is not null
                && installedStripped != "unknown"
                && !string.Equals(latestStripped, installedStripped, StringComparison.OrdinalIgnoreCase);

            return new UpdateInfo
            {
                InstalledVersion = installed,
                LatestVersion = latest,
                UpdateAvailable = updateAvailable,
                DownloadUrl = asset?.BrowserDownloadUrl,
                ReleaseNotes = TruncateNotes(release.Body),
                AssetSizeBytes = asset?.Size,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for MonaServer2 updates");
            return new UpdateInfo { InstalledVersion = installed };
        }
    }

    public async Task InstallUpdateAsync(string downloadUrl, string version, CancellationToken ct = default)
    {
        if (!await _installLock.WaitAsync(0, ct))
            throw new InvalidOperationException("An update install is already in progress.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"mona-update-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var ext = GetExtension(downloadUrl);
            var archivePath = Path.Combine(tempDir, "archive" + ext);

            Report("downloading", 0, "Starting download...");
            await DownloadWithProgressAsync(downloadUrl, archivePath, ct);

            Report("extracting", 75, "Extracting files...");
            await ExtractArchiveAsync(archivePath, tempDir, ct);

            Report("installing", 90, "Installing files...");
            var installDir = ResolveInstallDirectory();
            Directory.CreateDirectory(installDir);
            CopyBinaryFiles(tempDir, installDir);

            WriteVersionFile(version.TrimStart('v'));
            Report("done", 100, $"MonaServer2 {version} installed successfully.");
        }
        catch (OperationCanceledException)
        {
            Report("error", 0, "Update cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install MonaServer2 update");
            Report("error", 0, ex.Message);
            throw;
        }
        finally
        {
            _installLock.Release();
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private async Task DownloadWithProgressAsync(string url, string destPath, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;

            // Download phase maps to 0–70% of total progress
            var pct = total > 0 ? (int)(downloaded * 70L / total) : 5;
            Report("downloading", pct,
                $"Downloading... {FormatBytes(downloaded)}{(total > 0 ? $" / {FormatBytes(total)}" : "")}");
        }
    }

    private static async Task ExtractArchiveAsync(string archivePath, string destDir, CancellationToken ct)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true), ct);
        }
        else
        {
            // tar.gz or .tgz — use System.Formats.Tar (in-box since .NET 7)
            await using var gz = new GZipStream(File.OpenRead(archivePath), CompressionMode.Decompress);
            await System.Formats.Tar.TarFile.ExtractToDirectoryAsync(
                gz, destDir, overwriteFiles: true, cancellationToken: ct);
        }
    }

    private static void CopyBinaryFiles(string extractRoot, string installDir)
    {
        // If the archive produced a single top-level dir, descend into it
        var subdirs = Directory.GetDirectories(extractRoot);
        var rootFiles = Directory.GetFiles(extractRoot);
        var sourceDir = subdirs.Length == 1 && rootFiles.Length == 0 ? subdirs[0] : extractRoot;

        // Preserve user config/cert files that exist in the install dir
        var skipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ini", ".pem", ".pfx", ".p12" };
        var skipFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "VERSION" };

        var binaryName = OperatingSystem.IsWindows() ? "MonaServer.exe" : "MonaServer";
        var existingBinary = Path.Combine(installDir, binaryName);
        if (File.Exists(existingBinary))
            File.Copy(existingBinary, existingBinary + ".bak", overwrite: true);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (skipExtensions.Contains(Path.GetExtension(file))) continue;
            if (skipFileNames.Contains(Path.GetFileName(file))) continue;

            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(installDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            var binary = Path.Combine(installDir, binaryName);
            if (File.Exists(binary))
                File.SetUnixFileMode(binary,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{_settings.GitHubRepo}/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "MonaServer2-GUI/1.0");
        req.Headers.Add("Accept", "application/vnd.github+json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        return await resp.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: ct);
    }

    private GitHubAsset? SelectAsset(List<GitHubAsset> assets)
    {
        var pattern = OperatingSystem.IsWindows() ? _settings.WindowsAssetPattern
            : OperatingSystem.IsMacOS() ? _settings.MacOsAssetPattern
            : _settings.LinuxAssetPattern;

        return assets.FirstOrDefault(a =>
            a.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) &&
            (a.Name.EndsWith(".zip") || a.Name.EndsWith(".tar.gz") || a.Name.EndsWith(".tgz")));
    }

    private void Report(string phase, int pct, string? message) =>
        ProgressChanged?.Invoke(this, new UpdateProgress { Phase = phase, PercentComplete = pct, Message = message });

    private static string GetExtension(string url) =>
        url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz"
        : url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ? ".tgz"
        : ".zip";

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / 1_048_576.0:F1} MB",
    };

    private static string? TruncateNotes(string? notes) =>
        notes is { Length: > 500 } ? notes[..500] + "..." : notes;
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
