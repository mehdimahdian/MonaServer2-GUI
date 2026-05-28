using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MonaServer2.Core.OBS;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/obs/setup")]
public class OBSSetupController : ControllerBase
{
    private readonly OBSDetectionService _detection;
    private readonly ILogger<OBSSetupController> _logger;

    /* Plugin binaries are bundled next to the service binary in tools/obs-plugin/ */
    private static string PluginSourceDir =>
        Path.Combine(AppContext.BaseDirectory, "tools", "obs-plugin",
            OperatingSystem.IsWindows() ? "win-x64" :
            OperatingSystem.IsMacOS()   ? "osx-arm64" : "linux-x64");

    public OBSSetupController(OBSDetectionService detection, ILogger<OBSSetupController> logger)
    {
        _detection = detection;
        _logger    = logger;
    }

    [HttpGet("detect")]
    [ProducesResponseType<OBSInstallInfo>(200)]
    public IActionResult Detect() => Ok(_detection.Detect());

    [HttpPost("install-plugin")]
    [ProducesResponseType<OBSPluginInstallResult>(200)]
    [ProducesResponseType(400)]
    public IActionResult InstallPlugin([FromQuery] string? obsPath)
    {
        if (string.IsNullOrWhiteSpace(obsPath))
        {
            // Auto-detect
            var info = _detection.Detect();
            if (!info.IsInstalled)
                return BadRequest("OBS Studio is not installed and no path was provided.");
            obsPath = info.InstallPath!;
        }

        if (!Directory.Exists(PluginSourceDir))
            return BadRequest(
                $"Bundled OBS plugin not found at {PluginSourceDir}. " +
                "Please build the plugin from obs-plugin/ or download a pre-built release.");

        var result = _detection.InstallPlugin(obsPath, PluginSourceDir);
        return Ok(result);
    }

    [HttpGet("manual-instructions")]
    [ProducesResponseType<ManualInstallInstructions>(200)]
    public IActionResult GetManualInstructions()
    {
        var info = _detection.Detect();

        var pluginsDir = info.IsInstalled && info.PluginsDir is not null
            ? info.PluginsDir
            : GetDefaultPluginsDir();

        var dataDir = info.IsInstalled && info.DataDir is not null
            ? Path.Combine(info.DataDir, "obs-plugins", "obs-mona-live")
            : GetDefaultDataDir();

        return Ok(new ManualInstallInstructions
        {
            OBSFound         = info.IsInstalled,
            OBSInstallPath   = info.InstallPath,
            PluginBinaryDest = pluginsDir,
            PluginDataDest   = dataDir,
            PluginBinaryName = GetPluginBinaryName(),
            Steps = BuildSteps(pluginsDir, dataDir),
            DownloadUrl      = "https://github.com/mehdimahdian/MonaServer2-GUI/releases/latest",
        });
    }

    private static string GetPluginBinaryName() =>
        OperatingSystem.IsWindows() ? "obs-mona-live.dll" :
        OperatingSystem.IsMacOS()   ? "obs-mona-live.dylib" : "obs-mona-live.so";

    private static string GetDefaultPluginsDir()
    {
        if (OperatingSystem.IsWindows())
            return @"C:\Program Files\obs-studio\obs-plugins\64bit";
        if (OperatingSystem.IsMacOS())
            return "/Applications/OBS.app/Contents/PlugIns";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "obs-studio", "plugins", "obs-mona-live", "bin", "64bit");
    }

    private static string GetDefaultDataDir()
    {
        if (OperatingSystem.IsWindows())
            return @"C:\Program Files\obs-studio\data\obs-plugins\obs-mona-live";
        if (OperatingSystem.IsMacOS())
            return "/Applications/OBS.app/Contents/Resources/data/obs-plugins/obs-mona-live";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "obs-studio", "plugins", "obs-mona-live", "data");
    }

    private static List<string> BuildSteps(string pluginsDir, string dataDir)
    {
        var dll = GetPluginBinaryName();
        return
        [
            $"Download the latest release from https://github.com/mehdimahdian/MonaServer2-GUI/releases/latest",
            $"Extract the archive and locate the obs-plugin/ folder",
            $"Copy {dll} to: {pluginsDir}",
            $"Copy the data/locale/ folder to: {dataDir}",
            "Restart OBS Studio",
            "In OBS: Settings → Stream → Service → Mona Live Output",
        ];
    }
}

public record ManualInstallInstructions
{
    public bool OBSFound { get; init; }
    public string? OBSInstallPath { get; init; }
    public string PluginBinaryDest { get; init; } = "";
    public string PluginDataDest { get; init; } = "";
    public string PluginBinaryName { get; init; } = "";
    public List<string> Steps { get; init; } = [];
    public string DownloadUrl { get; init; } = "";
}
