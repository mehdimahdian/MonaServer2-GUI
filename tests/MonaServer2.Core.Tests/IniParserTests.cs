using FluentAssertions;
using MonaServer2.Core.Config;
using Xunit;

namespace MonaServer2.Core.Tests;

public class IniParserTests
{
    private const string SampleIni = """
        [server]
        name = TestServer
        threads = 4

        [HTTP]
        enabled = true
        host = 0.0.0.0
        port = 8080

        [RTMP]
        enabled = true
        port = 1935

        [SRT]
        enabled = false
        port = 9710
        latency = 2000

        [logs]
        level = 6
        directory = logs
        maxSize = 1000000
        rotation = 10
        """;

    [Fact]
    public void Parse_ReadsServerSection()
    {
        var path = WriteTempIni(SampleIni);
        var config = IniParser.Parse(path);

        config.Server.Name.Should().Be("TestServer");
        config.Server.Threads.Should().Be(4);
    }

    [Fact]
    public void Parse_ReadsHttpSection()
    {
        var path = WriteTempIni(SampleIni);
        var config = IniParser.Parse(path);

        config.Http.Enabled.Should().BeTrue();
        config.Http.Host.Should().Be("0.0.0.0");
        config.Http.Port.Should().Be(8080);
    }

    [Fact]
    public void Parse_ReadsSrtSection()
    {
        var path = WriteTempIni(SampleIni);
        var config = IniParser.Parse(path);

        config.Srt.Enabled.Should().BeFalse();
        config.Srt.Port.Should().Be(9710);
        config.Srt.Latency.Should().Be(2000);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var path = WriteTempIni(SampleIni);
        var original = IniParser.Parse(path);

        var outPath = Path.GetTempFileName();
        IniParser.Write(outPath, original);
        var reparsed = IniParser.Parse(outPath);

        reparsed.Server.Name.Should().Be(original.Server.Name);
        reparsed.Http.Port.Should().Be(original.Http.Port);
        reparsed.Rtmp.Port.Should().Be(original.Rtmp.Port);
        reparsed.Srt.Latency.Should().Be(original.Srt.Latency);

        File.Delete(outPath);
    }

    [Fact]
    public void Parse_IgnoresComments()
    {
        var iniWithComments = """
            ; This is a comment
            [server]
            name = CommentedServer ; inline comment
            # hash comment
            threads = 2
            """;

        var path = WriteTempIni(iniWithComments);
        var config = IniParser.Parse(path);

        config.Server.Name.Should().Be("CommentedServer");
        config.Server.Threads.Should().Be(2);
    }

    private static string WriteTempIni(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
