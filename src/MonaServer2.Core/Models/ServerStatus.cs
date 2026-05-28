namespace MonaServer2.Core.Models;

public record ServerStatus
{
    public bool IsRunning { get; init; }
    public string? Version { get; init; }
    public DateTime? StartedAt { get; init; }
    public TimeSpan Uptime => StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : TimeSpan.Zero;
    public int TotalConnections { get; init; }
    public int TotalPublications { get; init; }
    public Dictionary<string, int> ConnectionsByProtocol { get; init; } = [];
    public long TotalByteRateIn { get; init; }
    public long TotalByteRateOut { get; init; }
    public int ProcessId { get; init; }
}
