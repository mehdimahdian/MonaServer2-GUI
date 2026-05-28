namespace MonaServer2.Core.Streaming;

public class StreamingSettings
{
    public string FfmpegPath { get; set; } = "";
    public string DefaultRtmpUrl { get; set; } = "rtmp://localhost:1935/live";
    public string DefaultStreamKey { get; set; } = "stream";
    public int DefaultVideoBitrateKbps { get; set; } = 2500;
    public int PreviewFrameTimeoutMs { get; set; } = 5000;

    public string ResolvedFfmpegPath =>
        !string.IsNullOrWhiteSpace(FfmpegPath) ? FfmpegPath : FindFfmpeg();

    private static string FindFfmpeg()
    {
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        var toolsPath = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", name);
        if (File.Exists(toolsPath)) return toolsPath;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        return name;
    }
}
