namespace MonaServer2.Core.Models;

public record Publication
{
    public string Name { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string ClientAddress { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public TimeSpan Duration => DateTime.UtcNow - StartedAt;
    public int SubscriberCount { get; init; }
    public bool IsRecording { get; init; }
    public string? RecordingPath { get; init; }
    public long ByteRateIn { get; init; }
    public long ByteRateOut { get; init; }
    public double LostRateIn { get; init; }
    public double LostRateOut { get; init; }
    public List<StreamTrack> Tracks { get; init; } = [];
}
