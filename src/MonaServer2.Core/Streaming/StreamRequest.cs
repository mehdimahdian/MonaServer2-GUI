namespace MonaServer2.Core.Streaming;

public enum StreamSourceType { File, Calibration }

public record PushStreamRequest
{
    public StreamSourceType SourceType { get; init; } = StreamSourceType.File;
    public string? FilePath { get; init; }
    public string RtmpUrl { get; init; } = "rtmp://localhost:1935/live";
    public string StreamKey { get; init; } = "stream";
    public bool LoopFile { get; init; } = true;
    public int VideoBitrateKbps { get; init; } = 2500;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public int FrameRate { get; init; } = 25;
}

public record StreamStatus
{
    public bool IsStreaming { get; init; }
    public string? StreamKey { get; init; }
    public string? SourceDescription { get; init; }
    public DateTime? StartedAt { get; init; }
    public TimeSpan Uptime => StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : TimeSpan.Zero;
}
