namespace MonaServer2.Core.Models;

public record Session
{
    public string Id { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public DateTime ConnectedAt { get; init; }
    public TimeSpan Duration => DateTime.UtcNow - ConnectedAt;
    public string? PublishingTo { get; init; }
    public List<string> SubscribingTo { get; init; } = [];
    public long ByteRateIn { get; init; }
    public long ByteRateOut { get; init; }
}
