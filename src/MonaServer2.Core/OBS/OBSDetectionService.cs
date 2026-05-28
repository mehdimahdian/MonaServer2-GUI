using Microsoft.Extensions.Logging;

namespace MonaServer2.Core.OBS;

public record OBSInstallInfo
{
    public bool IsInstalled { get; init; }
    public string? InstallPath { get; init; }
    public string? Version { get; init; }
    public string? PluginsDir { get; init; }
    public string? DataDir { get; init; }
    public bool IsPluginInstalled { get; init; }
    public string? PluginVersion { get; init; }
}

public record OBSPluginInstallResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? InstalledPath { get; init; }
}

public class OBSDetectionService
{
    private const string PluginDllName   = "obs-mona-live.dll";
    private const string PluginSoName    = "obs-mona-live.so";
    private const string PluginDylibName = "obs-mona-live.dylib";
    private const string PluginVersionFile = "obs-mona-live.version";

    private readonly ILogger<OBSDetectionService> _logger;

    public OBSDetectionService(ILogger<OBSDetectionService> logger) => _logger = logger;

    public OBSInstallInfo Detect()
    {
        var obsPath = FindOBSInstallPath();
        if (obsPath is null)
            return new OBSInstallInfo { IsInstalled = false };

        var pluginsDir = GetPluginsDir(obsPath);
        var dataDir    = GetDataDir(obsPath);
        var pluginBinary = Path.Combine(pluginsDir, GetPluginBinaryName());
        var installed = File.Exists(pluginBinary);

        string? pluginVersion = null;
        if (installed)
        {
            var verFile = Path.Combine(dataDir, "obs-plugins", "obs-mona-live", PluginVersionFile);
            if (File.Exists(verFile))
                pluginVersion = File.ReadAllText(verFile).Trim();
        }

        return new OBSInstallInfo
        {
            IsInstalled      = true,
            InstallPath      = obsPath,
            PluginsDir       = pluginsDir,
            DataDir          = dataDir,
            IsPluginInstalled = installed,
            PluginVersion    = pluginVersion,
        };
    }

    public OBSPluginInstallResult InstallPlugin(string obsPath, string pluginSourceDir)
    {
        try
        {
            var pluginsDir = GetPluginsDir(obsPath);
            var dataDir    = GetDataDir(obsPath);
            var binaryName = GetPluginBinaryName();

            var srcBinary  = Path.Combine(pluginSourceDir, binaryName);
            if (!File.Exists(srcBinary))
                return new OBSPluginInstallResult { Success = false, Error = $"Plugin binary not found: {srcBinary}" };

            Directory.CreateDirectory(pluginsDir);
            var destBinary = Path.Combine(pluginsDir, binaryName);
            File.Copy(srcBinary, destBinary, overwrite: true);

            // Copy locale data
            var srcData  = Path.Combine(pluginSourceDir, "data");
            var destData = Path.Combine(dataDir, "obs-plugins", "obs-mona-live");
            if (Directory.Exists(srcData))
                CopyDirectory(srcData, destData);

            // Write version marker
            Directory.CreateDirectory(destData);
            File.WriteAllText(Path.Combine(destData, PluginVersionFile), "1.0.0");

            _logger.LogInformation("OBS plugin installed to {Path}", destBinary);
            return new OBSPluginInstallResult { Success = true, InstalledPath = destBinary };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install OBS plugin");
            return new OBSPluginInstallResult { Success = false, Error = ex.Message };
        }
    }

    /* ── Install path resolution ─────────────────────────────────────────── */

    private string? FindOBSInstallPath()
    {
        if (OperatingSystem.IsWindows()) return FindOBSWindows();
        if (OperatingSystem.IsMacOS())   return FindOBSMacOS();
        return FindOBSLinux();
    }

    private static string? FindOBSWindows()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio"),
            @"C:\obs-studio",
        };

        // Also try registry (HKLM\SOFTWARE\OBS Studio) — Windows only
#pragma warning disable CA1416
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\OBS Studio");
            if (key?.GetValue(null) is string regPath && Directory.Exists(regPath))
                return regPath;
        }
        catch { /* registry not available or key absent */ }
#pragma warning restore CA1416

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindOBSMacOS()
    {
        var candidates = new[]
        {
            "/Applications/OBS.app",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Applications", "OBS.app"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindOBSLinux()
    {
        var candidates = new[]
        {
            "/usr/lib/obs-plugins",
            "/usr/local/lib/obs-plugins",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "obs-studio"),
        };
        // Return parent of obs-plugins so callers can derive paths uniformly
        if (Directory.Exists("/usr/lib/obs-plugins")) return "/usr";
        if (Directory.Exists("/usr/local/lib/obs-plugins")) return "/usr/local";
        var userConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "obs-studio");
        return Directory.Exists(userConfig) ? userConfig : null;
    }

    private static string GetPluginsDir(string obsPath)
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(obsPath, "obs-plugins", "64bit");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(obsPath, "Contents", "PlugIns");
        // Linux: if obsPath ends in obs-studio (user config), use plugins subdir; else system lib
        var candidate = Path.Combine(obsPath, "lib", "obs-plugins");
        return Directory.Exists(candidate) ? candidate
            : Path.Combine(obsPath, "plugins");
    }

    private static string GetDataDir(string obsPath)
    {
        if (OperatingSystem.IsWindows())  return Path.Combine(obsPath, "data");
        if (OperatingSystem.IsMacOS())    return Path.Combine(obsPath, "Contents", "Resources", "data");
        return Path.Combine(obsPath, "share", "obs");
    }

    private static string GetPluginBinaryName()
    {
        if (OperatingSystem.IsWindows()) return PluginDllName;
        if (OperatingSystem.IsMacOS())   return PluginDylibName;
        return PluginSoName;
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel     = Path.GetRelativePath(src, file);
            var dstFile = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
            File.Copy(file, dstFile, overwrite: true);
        }
    }
}
