using Microsoft.AspNetCore.SignalR.Client;
using MonaServer2.Core.Models;
using MonaServer2.Core.Streaming;
using MonaServer2.Core.Update;

namespace MonaServer2.Desktop.Services;

public sealed class SignalRService : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<ServerStatus>? OnStatusChanged;
    public event Action<List<Publication>>? OnPublicationsUpdated;
    public event Action<List<Session>>? OnSessionsUpdated;
    public event Action<LogEntry>? OnLogReceived;
    public event Action<UpdateProgress>? OnUpdateProgress;
    public event Action<StreamStatus>? OnStreamingStatusChanged;

    public SignalRService(string serviceUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serviceUrl.TrimEnd('/')}/hub/monitor")
            .WithAutomaticReconnect()
            .Build();

        _connection.Closed += _ => { Disconnected?.Invoke(); return Task.CompletedTask; };
        _connection.Reconnected += _ => { Connected?.Invoke(); return Task.CompletedTask; };

        _connection.On<ServerStatus>("StatusChanged", s => OnStatusChanged?.Invoke(s));
        _connection.On<List<Publication>>("PublicationsUpdated", p => OnPublicationsUpdated?.Invoke(p));
        _connection.On<List<Session>>("SessionsUpdated", s => OnSessionsUpdated?.Invoke(s));
        _connection.On<LogEntry>("LogReceived", e => OnLogReceived?.Invoke(e));
        _connection.On<UpdateProgress>("UpdateProgress", p => OnUpdateProgress?.Invoke(p));
        _connection.On<StreamStatus>("StreamingStatusChanged", s => OnStreamingStatusChanged?.Invoke(s));
    }

    public async Task StartAsync()
    {
        try
        {
            await _connection.StartAsync();
            Connected?.Invoke();
        }
        catch
        {
            Disconnected?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
