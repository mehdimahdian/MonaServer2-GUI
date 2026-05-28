namespace MonaServer2.Core.Streaming;

public static class CalibrationArgs
{
    private const string BrandingName = "MonaServer2 GUI";

    /// <summary>
    /// Builds FFmpeg arguments that push SMPTE bars + sine tone with project branding as text overlays.
    /// </summary>
    public static string Build(PushStreamRequest req)
    {
        var size = $"{req.Width}x{req.Height}";
        var destination = $"{req.RtmpUrl.TrimEnd('/')}/{req.StreamKey}";

        // Use smptebars for the standard calibration pattern
        var inputArgs = $"-f lavfi -i \"smptebars=size={size}:rate={req.FrameRate}\" " +
                        $"-f lavfi -i \"sine=frequency=1000:sample_rate=44100\"";

        var bg = "box=1:boxcolor=black@0.55:boxborderw=5";

        // Text overlays — branding top-left, timecode bottom-right, tech info bottom-center, stream key bottom-left
        var filters = string.Join(",", new[]
        {
            $"drawtext=text='{Escape(BrandingName)}':fontsize=38:fontcolor=white@0.95:{bg}:x=22:y=18",
            $"drawtext=text='Calibration Stream':fontsize=22:fontcolor=yellow@0.90:{bg}:x=22:y=70",
            $"drawtext=text='{Escape(req.StreamKey)}':fontsize=18:fontcolor=cyan@0.85:{bg}:x=22:y=h-52",
            $"drawtext=text='%{{localtime\\:%Y-%m-%d  %H\\:%M\\:%S}}':fontsize=18:fontcolor=white@0.85:{bg}:x=w-340:y=h-52",
            $"drawtext=text='{size} @ {req.FrameRate}fps  |  {req.VideoBitrateKbps} kbps':fontsize=14:fontcolor=gray@0.80:x=(w-tw)/2:y=h-26",
        });

        return $"{inputArgs} " +
               $"-vf \"{filters}\" " +
               $"-c:v libx264 -preset ultrafast -tune zerolatency -b:v {req.VideoBitrateKbps}k " +
               $"-c:a aac -b:a 128k " +
               $"-f flv \"{destination}\"";
    }

    public static string BuildFileStream(PushStreamRequest req)
    {
        var loop = req.LoopFile ? "-stream_loop -1" : "";
        var destination = $"{req.RtmpUrl.TrimEnd('/')}/{req.StreamKey}";
        var filePath = req.FilePath ?? throw new ArgumentException("FilePath required for file stream.");

        return $"-re {loop} -i \"{filePath}\" " +
               $"-c:v libx264 -preset ultrafast -tune zerolatency -b:v {req.VideoBitrateKbps}k " +
               $"-c:a aac -b:a 128k " +
               $"-f flv \"{destination}\"";
    }

    // Escape single quotes and backslashes in drawtext values
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("'", "\\'").Replace(":", "\\:");
}
