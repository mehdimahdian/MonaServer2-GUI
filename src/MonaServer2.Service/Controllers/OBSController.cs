using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MonaServer2.Core.OBS;
using MonaServer2.Service.Hubs;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OBSController : ControllerBase
{
    private readonly IHubContext<OBSHub> _hub;

    public OBSController(IHubContext<OBSHub> hub) => _hub = hub;

    /* ── GET /api/obs/status ─────────────────────────────────────────────── */
    [HttpGet("status")]
    [ProducesResponseType<OBSSessionState>(200)]
    public IActionResult GetStatus() => Ok(OBSHub.GetCurrentState());

    /* ── POST /api/obs/command ───────────────────────────────────────────── */
    [HttpPost("command")]
    [ProducesResponseType(202)]
    public async Task<IActionResult> SendCommand(
        [FromBody] OBSRemoteCommand cmd,
        CancellationToken ct)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            type      = "remote_command",
            command   = cmd.Command,
            parameter = cmd.Parameter,
        });
        await _hub.Clients.All.SendAsync("ReceiveMessage", payload, ct);
        return Accepted();
    }

    /* ── POST /api/obs/ptz ───────────────────────────────────────────────── */
    [HttpPost("ptz")]
    [ProducesResponseType(202)]
    public async Task<IActionResult> SendPtz(
        [FromBody] PtzCommand ptz,
        CancellationToken ct)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            type  = "ptz",
            cmd   = ptz.Cmd,
            value = ptz.Value,
        });
        await _hub.Clients.All.SendAsync("ReceiveMessage", payload, ct);
        return Accepted();
    }

    /* ── POST /api/obs/scene ─────────────────────────────────────────────── */
    [HttpPost("scene")]
    [ProducesResponseType(202)]
    public async Task<IActionResult> SwitchScene(
        [FromQuery] string name,
        CancellationToken ct)
    {
        return await SendCommand(new OBSRemoteCommand { Command = "set_scene", Parameter = name }, ct);
    }

    /* ── POST /api/obs/record/start | stop ───────────────────────────────── */
    [HttpPost("record/start")]
    [ProducesResponseType(202)]
    public async Task<IActionResult> StartRecord(CancellationToken ct)
        => await SendCommand(new OBSRemoteCommand { Command = "start_recording" }, ct);

    [HttpPost("record/stop")]
    [ProducesResponseType(202)]
    public async Task<IActionResult> StopRecord(CancellationToken ct)
        => await SendCommand(new OBSRemoteCommand { Command = "stop_recording" }, ct);

    /* ── WHIP endpoint (proxied to MonaServer2) ──────────────────────────── */
    [HttpPost("whip/{streamKey}")]
    [Consumes("application/sdp")]
    [ProducesResponseType(201)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> WhipIngest(
        [FromRoute] string streamKey,
        CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var sdpOffer = await reader.ReadToEndAsync(ct);

        /*
         * Forward the SDP offer to MonaServer2's WHIP endpoint.
         * If MonaServer2 supports WHIP natively, proxy directly:
         *   POST http://localhost:80/webrtc/{streamKey}
         * Return the SDP answer + Location header.
         *
         * For now we return a stub answer to validate the plugin signalling path.
         */
        var resourceUrl = $"{Request.Scheme}://{Request.Host}/api/obs/whip/{streamKey}";
        Response.Headers.Location = resourceUrl;
        Response.ContentType = "application/sdp";

        var sdpAnswer = BuildSdpAnswer(sdpOffer, streamKey);
        return StatusCode(201, sdpAnswer);
    }

    [HttpDelete("whip/{streamKey}")]
    [ProducesResponseType(200)]
    public IActionResult WhipDelete([FromRoute] string streamKey)
    {
        // Signal MonaServer2 to tear down the WebRTC session
        return Ok();
    }

    private static string BuildSdpAnswer(string offer, string key)
    {
        // Minimal SDP answer — in production, forward to MonaServer2's WHIP handler
        return "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=Mona Live\r\nt=0 0\r\n";
    }
}
