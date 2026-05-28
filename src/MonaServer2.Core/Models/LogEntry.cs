namespace MonaServer2.Core.Models;

public enum LogLevel
{
    Fatal,
    Critic,
    Error,
    Warn,
    Note,
    Info,
    Debug,
    Trace
}

public record LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public LogLevel Level { get; init; } = LogLevel.Info;
    public string Message { get; init; } = string.Empty;
    public string? Source { get; init; }

    public static LogEntry Parse(string line)
    {
        // MonaServer2 log format: [LEVEL] source | message
        // e.g. "[INFO] HTTP | Server started on port 80"
        var level = LogLevel.Info;
        string? source = null;
        var message = line;

        if (line.StartsWith('['))
        {
            var closeBracket = line.IndexOf(']');
            if (closeBracket > 0)
            {
                var levelStr = line[1..closeBracket].Trim();
                level = levelStr.ToUpperInvariant() switch
                {
                    "FATAL"  => LogLevel.Fatal,
                    "CRITIC" => LogLevel.Critic,
                    "ERROR"  => LogLevel.Error,
                    "WARN"   => LogLevel.Warn,
                    "NOTE"   => LogLevel.Note,
                    "INFO"   => LogLevel.Info,
                    "DEBUG"  => LogLevel.Debug,
                    "TRACE"  => LogLevel.Trace,
                    _        => LogLevel.Info
                };
                message = line[(closeBracket + 1)..].Trim();
            }
        }

        var pipeIndex = message.IndexOf('|');
        if (pipeIndex > 0)
        {
            source = message[..pipeIndex].Trim();
            message = message[(pipeIndex + 1)..].Trim();
        }

        return new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Source = source,
            Message = message
        };
    }
}
