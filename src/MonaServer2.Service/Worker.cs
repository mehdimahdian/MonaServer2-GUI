using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MonaServer2.Core.Api;
using MonaServer2.Core.Models;
using MonaServer2.Core.Process;
using MonaServer2.Core.Streaming;
using MonaServer2.Core.Update;
using MonaServer2.Service.Hubs;

namespace MonaServer2.Service;

public sealed class Worker : BackgroundService
{
    private readonly MonaServerProcess _monaProcess;
    private readonly MonaApiClient _apiClient;
    private readonly IHubContext<MonitorHub> _hub;
    private readonly ILogger<Worker> _logger;
    private readonly MonaServerSettings _settings;

    private bool _autoRestartEnabled;
    private DateTime? _processStartedAt;

    public bool IsRunning => _monaProcess.IsRunning;

    public Worker(
        MonaServerProcess monaProcess,
        MonaApiClient apiClient,
        IHubContext<MonitorHub> hub,
        BinaryUpdateService updateService,
        StreamingProcess streamingProcess,
        IOptions<MonaServerSettings> settings,
        ILogger<Worker> logger)
    {
        _monaProcess = monaProcess;
        _apiClient = apiClient;
        _hub = hub;
        _logger = logger;
        _settings = settings.Value;
        _autoRestartEnabled = _settings.AutoRestart;

        updateService.ProgressChanged += OnUpdateProgress;
        streamingProcess.StatusChanged += OnStreamingStatusChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _monaProcess.LogReceived += OnLogReceived;
        _monaProcess.ProcessExited += OnProcessExited;

        if (_settings.AutoStart)
            await TryStartMonaServerAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAndBroadcastAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(_settings.PollIntervalMs, stoppingToken).ConfigureAwait(false);
        }

        _monaProcess.LogReceived -= OnLogReceived;
        _monaProcess.ProcessExited -= OnProcessExited;
        await _monaProcess.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task StartMonaServerAsync(CancellationToken ct = default)
    {
        await TryStartMonaServerAsync(ct).ConfigureAwait(false);
    }

    public async Task StopMonaServerAsync(CancellationToken ct = default)
    {
        _autoRestartEnabled = false;
        await _monaProcess.StopAsync(ct).ConfigureAwait(false);
        _processStartedAt = null;
        await BroadcastStatusAsync(ct).ConfigureAwait(false);
    }

    public async Task RestartMonaServerAsync(CancellationToken ct = default)
    {
        var exe = _settings.ResolvedExecutablePath;
        var wd = ResolveWorkingDirectory(exe);
        await _monaProcess.RestartAsync(exe, wd, ct).ConfigureAwait(false);
        _processStartedAt = DateTime.UtcNow;
        _autoRestartEnabled = _settings.AutoRestart;
    }

    private async Task TryStartMonaServerAsync(CancellationToken ct)
    {
        try
        {
            var exe = _settings.ResolvedExecutablePath;
            var wd = ResolveWorkingDirectory(exe);
            await _monaProcess.StartAsync(exe, wd, ct).ConfigureAwait(false);
            _processStartedAt = DateTime.UtcNow;
            _autoRestartEnabled = _settings.AutoRestart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MonaServer2");
        }
    }

    private async Task PollAndBroadcastAsync(CancellationToken ct)
    {
        if (!_monaProcess.IsRunning) return;

        var pubsTask = _apiClient.GetPublicationsAsync(ct);
        var sessionsTask = _apiClient.GetSessionsAsync(ct);
        await Task.WhenAll(pubsTask, sessionsTask).ConfigureAwait(false);
        var pubs = pubsTask.Result;
        var sessions = sessionsTask.Result;

        await Task.WhenAll(
            _hub.Clients.All.SendAsync("PublicationsUpdated", pubs, ct),
            _hub.Clients.All.SendAsync("SessionsUpdated", sessions, ct),
            BroadcastStatusAsync(ct)
        ).ConfigureAwait(false);
    }

    private async Task BroadcastStatusAsync(CancellationToken ct)
    {
        var status = _monaProcess.IsRunning
            ? await _apiClient.GetStatusAsync(ct).ConfigureAwait(false) with
              {
                  IsRunning = true,
                  StartedAt = _processStartedAt,
                  ProcessId = _monaProcess.ProcessId ?? 0
              }
            : new ServerStatus { IsRunning = false };

        await _hub.Clients.All.SendAsync("StatusChanged", status, ct).ConfigureAwait(false);
    }

    private void OnLogReceived(object? sender, LogEntry entry)
    {
        _ = _hub.Clients.All.SendAsync("LogReceived", entry);
    }

    private void OnUpdateProgress(object? sender, UpdateProgress progress)
    {
        _ = _hub.Clients.All.SendAsync("UpdateProgress", progress);
    }

    private void OnStreamingStatusChanged(object? sender, StreamStatus status)
    {
        _ = _hub.Clients.All.SendAsync("StreamingStatusChanged", status);
    }

    private void OnProcessExited(object? sender, int exitCode)
    {
        _processStartedAt = null;
        _ = _hub.Clients.All.SendAsync("StatusChanged", new ServerStatus { IsRunning = false });

        if (_autoRestartEnabled && exitCode != 0)
        {
            _logger.LogWarning("MonaServer2 crashed (exit {Code}), restarting in {Delay}ms", exitCode, _settings.RestartDelayMs);
            _ = RestartAfterDelayAsync();
        }
    }

    private async Task RestartAfterDelayAsync()
    {
        await Task.Delay(_settings.RestartDelayMs).ConfigureAwait(false);
        await TryStartMonaServerAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static string ResolveWorkingDirectory(string executablePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        return dir ?? AppContext.BaseDirectory;
    }
}
