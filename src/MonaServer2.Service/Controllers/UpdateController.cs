using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MonaServer2.Core.Update;
using MonaServer2.Service.Hubs;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UpdateController : ControllerBase
{
    private readonly BinaryUpdateService _updateService;
    private readonly Worker _worker;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(BinaryUpdateService updateService, Worker worker, ILogger<UpdateController> logger)
    {
        _updateService = updateService;
        _worker = worker;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<InstalledVersionResponse>(200)]
    public IActionResult GetInstalledVersion() =>
        Ok(new InstalledVersionResponse(_updateService.InstalledVersion, _updateService.ResolveInstallDirectory()));

    [HttpGet("check")]
    [ProducesResponseType<UpdateInfo>(200)]
    public async Task<ActionResult<UpdateInfo>> CheckForUpdate(CancellationToken ct) =>
        Ok(await _updateService.CheckForUpdateAsync(ct));

    [HttpPost("install")]
    [ProducesResponseType(202)]
    [ProducesResponseType(409)]
    public IActionResult Install([FromBody] InstallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DownloadUrl))
            return BadRequest("DownloadUrl is required.");

        _ = RunInstallAsync(request.DownloadUrl, request.Version, request.RestartAfterInstall);
        return Accepted();
    }

    private async Task RunInstallAsync(string downloadUrl, string version, bool restartAfter)
    {
        var wasRunning = _worker.IsRunning;

        if (wasRunning)
        {
            _logger.LogInformation("Stopping MonaServer2 before update install");
            await _worker.StopMonaServerAsync(CancellationToken.None);
        }

        try
        {
            await _updateService.InstallUpdateAsync(downloadUrl, version, CancellationToken.None);
        }
        finally
        {
            if (wasRunning && restartAfter)
            {
                _logger.LogInformation("Restarting MonaServer2 after update install");
                await _worker.StartMonaServerAsync(CancellationToken.None);
            }
        }
    }
}

public record InstalledVersionResponse(string Version, string InstallDirectory);

public record InstallRequest
{
    public string DownloadUrl { get; init; } = "";
    public string Version { get; init; } = "unknown";
    public bool RestartAfterInstall { get; init; } = true;
}
