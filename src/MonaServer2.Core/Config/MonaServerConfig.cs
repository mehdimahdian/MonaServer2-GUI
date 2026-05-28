namespace MonaServer2.Core.Config;

public class MonaServerConfig
{
    public ServerSection Server { get; set; } = new();
    public HttpSection Http { get; set; } = new();
    public RtmpSection Rtmp { get; set; } = new();
    public SrtSection Srt { get; set; } = new();
    public WebSocketSection WebSocket { get; set; } = new();
    public RtmfpSection Rtmfp { get; set; } = new();
    public LogSection Log { get; set; } = new();
    public PathsSection Paths { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> RawSections { get; set; } = [];
}

public class ServerSection
{
    public string Name { get; set; } = "MonaServer";
    public int Threads { get; set; } = 0;
    public int SocketReceiveSize { get; set; } = 0;
    public int SocketSendSize { get; set; } = 0;
}

public class ProtocolSection
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; }
    public string? CertificatePath { get; set; }
    public string? KeyPath { get; set; }
}

public class HttpSection : ProtocolSection
{
    public HttpSection() { Port = 80; }
    public int TlsPort { get; set; } = 443;
    public bool TlsEnabled { get; set; }
}

public class RtmpSection : ProtocolSection
{
    public RtmpSection() { Port = 1935; }
}

public class SrtSection : ProtocolSection
{
    public SrtSection() { Port = 9710; }
    public int Latency { get; set; } = 2000;
    public string? Passphrase { get; set; }
    public int PbKeyLen { get; set; } = 16;
}

public class WebSocketSection : ProtocolSection
{
    public WebSocketSection() { Port = 80; }
}

public class RtmfpSection : ProtocolSection
{
    public RtmfpSection() { Port = 1935; }
}

public class LogSection
{
    public string Level { get; set; } = "6";
    public string Directory { get; set; } = "logs";
    public int MaxSize { get; set; } = 1000000;
    public int Rotation { get; set; } = 10;
}

public class PathsSection
{
    public string Www { get; set; } = "www";
    public string Data { get; set; } = "data";
}
