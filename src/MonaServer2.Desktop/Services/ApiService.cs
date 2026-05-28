using MonaServer2.Core.Config;
using MonaServer2.Core.Models;
using MonaServer2.Core.Streaming;
using MonaServer2.Core.Update;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MonaServer2.Desktop.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiService(HttpClient http) => _http = http;

    public async Task<ServerStatus> GetStatusAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ServerStatus>("/api/status", JsonOpts)
                   ?? new ServerStatus();
        }
        catch { return new ServerStatus(); }
    }

    public async Task<List<Publication>> GetPublicationsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Publication>>("/api/publications", JsonOpts)
                   ?? [];
        }
        catch { return []; }
    }

    public async Task<List<Session>> GetSessionsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<Session>>("/api/sessions", JsonOpts)
                   ?? [];
        }
        catch { return []; }
    }

    public async Task<MonaServerConfig> GetConfigAsync()
    {
        var response = await _http.GetFromJsonAsync<MonaServerConfig>("/api/config", JsonOpts);
        return response ?? new MonaServerConfig();
    }

    public async Task<string> GetConfigRawAsync()
    {
        var response = await _http.GetAsync("/api/config/raw");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task SaveConfigAsync(MonaServerConfig config)
    {
        var response = await _http.PutAsJsonAsync("/api/config", config, JsonOpts);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveConfigRawAsync(string rawIni)
    {
        var content = new StringContent($"\"{JsonEncodedText.Encode(rawIni)}\"", Encoding.UTF8, "application/json");
        var response = await _http.PutAsync("/api/config/raw", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task StartProcessAsync()
    {
        var response = await _http.PostAsync("/api/process/start", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopProcessAsync()
    {
        var response = await _http.PostAsync("/api/process/stop", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task RestartProcessAsync()
    {
        var response = await _http.PostAsync("/api/process/restart", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<UpdateInfo> CheckUpdateAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<UpdateInfo>("/api/update/check", JsonOpts)
                   ?? new UpdateInfo();
        }
        catch { return new UpdateInfo(); }
    }

    public async Task<string> GetInstalledVersionAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<InstalledVersionDto>("/api/update", JsonOpts);
            return resp?.Version ?? "unknown";
        }
        catch { return "unknown"; }
    }

    public async Task InstallUpdateAsync(string downloadUrl, string version, bool restartAfter = true)
    {
        var response = await _http.PostAsJsonAsync("/api/update/install",
            new { DownloadUrl = downloadUrl, Version = version, RestartAfterInstall = restartAfter },
            JsonOpts);
        response.EnsureSuccessStatusCode();
    }

    private record InstalledVersionDto(string Version, string InstallDirectory);

    // OBS Setup
    public async Task<OBSInstallInfo> DetectOBSAsync()
    {
        try { return await _http.GetFromJsonAsync<OBSInstallInfo>("/api/obs/setup/detect", JsonOpts) ?? new OBSInstallInfo(); }
        catch { return new OBSInstallInfo(); }
    }

    public async Task<ManualInstallInstructions?> GetOBSManualInstructionsAsync()
    {
        try { return await _http.GetFromJsonAsync<ManualInstallInstructions>("/api/obs/setup/manual-instructions", JsonOpts); }
        catch { return null; }
    }

    public async Task<OBSPluginInstallResult> InstallOBSPluginAsync(string? obsPath = null)
    {
        var url = string.IsNullOrWhiteSpace(obsPath)
            ? "/api/obs/setup/install-plugin"
            : $"/api/obs/setup/install-plugin?obsPath={Uri.EscapeDataString(obsPath)}";
        var response = await _http.PostAsync(url, null);
        return await response.Content.ReadFromJsonAsync<OBSPluginInstallResult>(JsonOpts)
               ?? new OBSPluginInstallResult();
    }

    // Stub record — mirrors MonaServer2.Core.OBS types without taking project reference
    public record OBSInstallInfo(
        bool IsInstalled = false, string? InstallPath = null, string? Version = null,
        string? PluginsDir = null, string? DataDir = null,
        bool IsPluginInstalled = false, string? PluginVersion = null);

    public record OBSPluginInstallResult(bool Success = false, string? Error = null, string? InstalledPath = null);

    public record ManualInstallInstructions(
        bool OBSFound = false, string? OBSInstallPath = null,
        string PluginBinaryDest = "", string PluginDataDest = "",
        string PluginBinaryName = "", List<string>? Steps = null, string DownloadUrl = "");

    public async Task<StreamStatus> GetStreamStatusAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<StreamStatus>("/api/pusher/status", JsonOpts)
                   ?? new StreamStatus();
        }
        catch { return new StreamStatus(); }
    }

    public async Task StartStreamAsync(PushStreamRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/pusher/start", request, JsonOpts);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopStreamAsync()
    {
        var response = await _http.PostAsync("/api/pusher/stop", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]?> GetPreviewFrameAsync(string streamUrl, int width = 640, int height = 360, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/pusher/preview?url={Uri.EscapeDataString(streamUrl)}&width={width}&height={height}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch { return null; }
    }
}
