using Microsoft.Extensions.Logging;
using MonaServer2.Core.Models;
using System.Diagnostics;

namespace MonaServer2.Core.Process;

public sealed class MonaServerProcess : IDisposable
{
    private readonly ILogger<MonaServerProcess> _logger;
    private System.Diagnostics.Process? _process;
    private bool _disposed;

    public event EventHandler<LogEntry>? LogReceived;
    public event EventHandler<int>? ProcessExited;

    public bool IsRunning => _process is { HasExited: false };
    public int? ProcessId => IsRunning ? _process?.Id : null;

    public MonaServerProcess(ILogger<MonaServerProcess> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(string executablePath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("MonaServer2 is already running.");

        if (!File.Exists(executablePath))
            throw new FileNotFoundException($"MonaServer2 binary not found: {executablePath}");

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnOutputData;
        _process.ErrorDataReceived += OnOutputData;
        _process.Exited += OnExited;

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _logger.LogInformation("MonaServer2 started (PID {Pid})", _process.Id);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null || _process.HasExited)
            return;

        _logger.LogInformation("Stopping MonaServer2 (PID {Pid})", _process.Id);

        try
        {
            // Graceful on Unix; CloseMainWindow on Windows
            if (OperatingSystem.IsWindows())
                _process.CloseMainWindow();
            else
                _process.Kill(entireProcessTree: false); // SIGTERM equivalent via Kill on non-Windows

            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Error during graceful stop; forcing kill");
            _process.Kill(entireProcessTree: true);
        }
    }

    public async Task RestartAsync(string executablePath, string workingDirectory, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        await StartAsync(executablePath, workingDirectory, cancellationToken).ConfigureAwait(false);
    }

    private void OnOutputData(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        var entry = LogEntry.Parse(e.Data);
        LogReceived?.Invoke(this, entry);
    }

    private void OnExited(object? sender, EventArgs e)
    {
        var code = _process?.ExitCode ?? -1;
        _logger.LogWarning("MonaServer2 exited with code {Code}", code);
        ProcessExited?.Invoke(this, code);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _process?.Dispose();
    }
}
