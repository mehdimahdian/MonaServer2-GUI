using Microsoft.AspNetCore.Mvc;
using MonaServer2.Core.Api;
using MonaServer2.Core.Models;
using MonaServer2.Core.Process;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly MonaApiClient _api;
    private readonly MonaServerProcess _process;

    public StatusController(MonaApiClient api, MonaServerProcess process)
    {
        _api = api;
        _process = process;
    }

    [HttpGet]
    [ProducesResponseType<ServerStatus>(200)]
    public async Task<ServerStatus> Get(CancellationToken ct)
    {
        if (!_process.IsRunning)
            return new ServerStatus { IsRunning = false };

        var status = await _api.GetStatusAsync(ct);
        return status with { IsRunning = true, ProcessId = _process.ProcessId ?? 0 };
    }
}
