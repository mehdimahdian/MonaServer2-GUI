using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MonaServer2.Core.Streaming;

public sealed class StreamingProcess : IDisposable
{
    private readonly ILogger<StreamingProcess> _logger;
    private System.Diagnostics.Process? _process;
    private bool _disposed;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<StreamStatus>? StatusChanged;

    public bool IsStreaming => _process is { HasExited: false };
    public StreamStatus CurrentStatus { get; private set; } = new();

    public StreamingProcess(ILogger<StreamingProcess> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(string ffmpegPath, string arguments, string streamKey, string sourceDescription, CancellationToken ct = default)
    {
        if (IsStreaming)
            throw new InvalidOperationException("A stream is already in progress.");

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnOutput;
        _process.ErrorDataReceived += OnOutput;
        _process.Exited += OnExited;

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        CurrentStatus = new StreamStatus
        {
            IsStreaming = true,
            StreamKey = streamKey,
            SourceDescription = sourceDescription,
            StartedAt = DateTime.UtcNow,
        };

        _logger.LogInformation("FFmpeg stream started (PID {Pid}) → {Key}", _process.Id, streamKey);
        StatusChanged?.Invoke(this, CurrentStatus);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
            return;

        _logger.LogInformation("Stopping FFmpeg stream (PID {Pid})", _process.Id);

        try
        {
            if (OperatingSystem.IsWindows())
                _process.CloseMainWindow();
            else
                _process.Kill(entireProcessTree: false);

            await _process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch when (!ct.IsCancellationRequested)
        {
            _process.Kill(entireProcessTree: true);
        }
    }

    private void OnOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        OutputReceived?.Invoke(this, e.Data);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        _logger.LogInformation("FFmpeg stream exited");
        CurrentStatus = new StreamStatus { IsStreaming = false };
        StatusChanged?.Invoke(this, CurrentStatus);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _process?.Dispose();
    }
}
