namespace MonaServer2.Core.Models;

public enum TrackType { Video, Audio, Data }

public record StreamTrack
{
    public TrackType Type { get; init; }
    public string Codec { get; init; } = string.Empty;
    public string? Language { get; init; }

    // Video
    public int Width { get; init; }
    public int Height { get; init; }
    public double Fps { get; init; }

    // Audio
    public int SampleRate { get; init; }
    public int Channels { get; init; }

    public long Bitrate { get; init; }
}
