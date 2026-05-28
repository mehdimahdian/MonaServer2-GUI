using Microsoft.AspNetCore.Mvc;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessController : ControllerBase
{
    private readonly Worker _worker;

    public ProcessController(Worker worker) => _worker = worker;

    [HttpPost("start")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        await _worker.StartMonaServerAsync(ct);
        return NoContent();
    }

    [HttpPost("stop")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        await _worker.StopMonaServerAsync(ct);
        return NoContent();
    }

    [HttpPost("restart")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Restart(CancellationToken ct)
    {
        await _worker.RestartMonaServerAsync(ct);
        return NoContent();
    }
}
