using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MonaServer2.Core.Streaming;
using System.Diagnostics;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PusherController : ControllerBase
{
    private readonly StreamingProcess _streaming;
    private readonly StreamingSettings _settings;
    private readonly ILogger<PusherController> _logger;

    public PusherController(StreamingProcess streaming, IOptions<StreamingSettings> settings, ILogger<PusherController> logger)
    {
        _streaming = streaming;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    [ProducesResponseType<StreamStatus>(200)]
    public IActionResult GetStatus() => Ok(_streaming.CurrentStatus);

    [HttpPost("start")]
    [ProducesResponseType(204)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Start([FromBody] PushStreamRequest request, CancellationToken ct)
    {
        if (_streaming.IsStreaming)
            return Conflict("A stream is already running. Stop it first.");

        var ffmpeg = _settings.ResolvedFfmpegPath;

        string args;
        string description;

        if (request.SourceType == StreamSourceType.Calibration)
        {
            args = CalibrationArgs.Build(request);
            description = $"Calibration pattern ({request.Width}x{request.Height}@{request.FrameRate})";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.FilePath) || !System.IO.File.Exists(request.FilePath))
                return BadRequest("FilePath must point to an existing file.");

            args = CalibrationArgs.BuildFileStream(request);
            description = $"File: {Path.GetFileName(request.FilePath)}";
        }

        _logger.LogInformation("Starting push stream: {Desc} → {Url}/{Key}", description, request.RtmpUrl, request.StreamKey);

        await _streaming.StartAsync(ffmpeg, args, request.StreamKey, description, ct);
        return NoContent();
    }

    [HttpPost("stop")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        await _streaming.StopAsync(ct);
        return NoContent();
    }

    [HttpGet("preview")]
    [ProducesResponseType<FileResult>(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetPreviewFrame(
        [FromQuery] string url,
        [FromQuery] int width = 640,
        [FromQuery] int height = 360,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("url is required.");

        var ffmpeg = _settings.ResolvedFfmpegPath;

        // Single-frame grab: read from the stream URL and output one JPEG via stdout
        var args = $"-timeout {_settings.PreviewFrameTimeoutMs * 1000} " +
                   $"-i \"{url}\" " +
                   $"-vframes 1 -f image2 -vcodec mjpeg -q:v 3 " +
                   $"-vf \"scale={width}:{height}\" pipe:1";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)!;

        using var ms = new MemoryStream();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_settings.PreviewFrameTimeoutMs + 2000);

        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(ms, cts.Token);
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return StatusCode(408, "Preview timed out.");
        }

        if (ms.Length == 0)
            return NotFound("Could not grab a preview frame from the given URL.");

        return File(ms.ToArray(), "image/jpeg");
    }
}
