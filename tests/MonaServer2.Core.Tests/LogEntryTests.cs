using FluentAssertions;
using MonaServer2.Core.Models;
using Xunit;

namespace MonaServer2.Core.Tests;

public class LogEntryTests
{
    [Theory]
    [InlineData("[INFO] HTTP | Server started on port 80", LogLevel.Info, "HTTP", "Server started on port 80")]
    [InlineData("[ERROR] RTMP | Connection refused", LogLevel.Error, "RTMP", "Connection refused")]
    [InlineData("[WARN] SRT | High packet loss", LogLevel.Warn, "SRT", "High packet loss")]
    [InlineData("[DEBUG] Core | Timer fired", LogLevel.Debug, "Core", "Timer fired")]
    public void Parse_RecognizesLevelAndSource(string line, LogLevel level, string source, string message)
    {
        var entry = LogEntry.Parse(line);

        entry.Level.Should().Be(level);
        entry.Source.Should().Be(source);
        entry.Message.Should().Be(message);
    }

    [Fact]
    public void Parse_HandlesUnformattedLine()
    {
        var entry = LogEntry.Parse("just a raw log line");

        entry.Level.Should().Be(LogLevel.Info);
        entry.Message.Should().Be("just a raw log line");
    }

    [Fact]
    public void Parse_SetsTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entry = LogEntry.Parse("[INFO] Test | message");
        var after = DateTime.UtcNow.AddSeconds(1);

        entry.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }
}
