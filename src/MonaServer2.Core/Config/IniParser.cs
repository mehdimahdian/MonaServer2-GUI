using System.Text;
using System.Text.RegularExpressions;

namespace MonaServer2.Core.Config;

public static class IniParser
{
    private static readonly Regex SectionRegex = new(@"^\[(.+)\]", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^([^=;#]+)=(.*)$", RegexOptions.Compiled);

    public static Dictionary<string, Dictionary<string, string>> ParseRaw(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = "__global__";
        result[current] = [];

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            var sectionMatch = SectionRegex.Match(line);
            if (sectionMatch.Success)
            {
                current = sectionMatch.Groups[1].Value.Trim();
                result.TryAdd(current, []);
                continue;
            }

            var kvMatch = KeyValueRegex.Match(line);
            if (kvMatch.Success)
            {
                var key = kvMatch.Groups[1].Value.Trim();
                var value = kvMatch.Groups[2].Value.Trim();
                var commentIdx = value.IndexOf(';');
                if (commentIdx >= 0)
                    value = value[..commentIdx].Trim();
                result[current][key] = value;
            }
        }

        return result;
    }

    public static MonaServerConfig Parse(string path)
    {
        var raw = ParseRaw(path);
        var config = new MonaServerConfig { RawSections = raw };

        Apply(raw, "server", s =>
        {
            config.Server.Name = Get(s, "name", config.Server.Name);
            config.Server.Threads = GetInt(s, "threads", config.Server.Threads);
            config.Server.SocketReceiveSize = GetInt(s, "socketReceiveSize", config.Server.SocketReceiveSize);
            config.Server.SocketSendSize = GetInt(s, "socketSendSize", config.Server.SocketSendSize);
        });

        Apply(raw, "HTTP", s =>
        {
            config.Http.Enabled = GetBool(s, "enabled", config.Http.Enabled);
            config.Http.Host = Get(s, "host", config.Http.Host);
            config.Http.Port = GetInt(s, "port", config.Http.Port);
            config.Http.TlsPort = GetInt(s, "tlsPort", config.Http.TlsPort);
            config.Http.TlsEnabled = GetBool(s, "tlsEnabled", config.Http.TlsEnabled);
            config.Http.CertificatePath = GetNullable(s, "certificate");
            config.Http.KeyPath = GetNullable(s, "key");
        });

        Apply(raw, "RTMP", s =>
        {
            config.Rtmp.Enabled = GetBool(s, "enabled", config.Rtmp.Enabled);
            config.Rtmp.Host = Get(s, "host", config.Rtmp.Host);
            config.Rtmp.Port = GetInt(s, "port", config.Rtmp.Port);
        });

        Apply(raw, "SRT", s =>
        {
            config.Srt.Enabled = GetBool(s, "enabled", config.Srt.Enabled);
            config.Srt.Host = Get(s, "host", config.Srt.Host);
            config.Srt.Port = GetInt(s, "port", config.Srt.Port);
            config.Srt.Latency = GetInt(s, "latency", config.Srt.Latency);
            config.Srt.Passphrase = GetNullable(s, "passphrase");
            config.Srt.PbKeyLen = GetInt(s, "pbKeyLen", config.Srt.PbKeyLen);
        });

        Apply(raw, "logs", s =>
        {
            config.Log.Level = Get(s, "level", config.Log.Level);
            config.Log.Directory = Get(s, "directory", config.Log.Directory);
            config.Log.MaxSize = GetInt(s, "maxSize", config.Log.MaxSize);
            config.Log.Rotation = GetInt(s, "rotation", config.Log.Rotation);
        });

        Apply(raw, "paths", s =>
        {
            config.Paths.Www = Get(s, "www", config.Paths.Www);
            config.Paths.Data = Get(s, "data", config.Paths.Data);
        });

        return config;
    }

    public static void Write(string path, MonaServerConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[server]");
        sb.AppendLine($"name = {config.Server.Name}");
        if (config.Server.Threads > 0)
            sb.AppendLine($"threads = {config.Server.Threads}");
        if (config.Server.SocketReceiveSize > 0)
            sb.AppendLine($"socketReceiveSize = {config.Server.SocketReceiveSize}");
        if (config.Server.SocketSendSize > 0)
            sb.AppendLine($"socketSendSize = {config.Server.SocketSendSize}");
        sb.AppendLine();

        WriteProtocol(sb, "HTTP", config.Http, extra: s =>
        {
            if (config.Http.TlsEnabled)
            {
                s.AppendLine($"tlsPort = {config.Http.TlsPort}");
                s.AppendLine($"tlsEnabled = true");
                if (config.Http.CertificatePath != null)
                    s.AppendLine($"certificate = {config.Http.CertificatePath}");
                if (config.Http.KeyPath != null)
                    s.AppendLine($"key = {config.Http.KeyPath}");
            }
        });

        WriteProtocol(sb, "RTMP", config.Rtmp);

        sb.AppendLine("[SRT]");
        sb.AppendLine($"enabled = {config.Srt.Enabled.ToString().ToLower()}");
        sb.AppendLine($"host = {config.Srt.Host}");
        sb.AppendLine($"port = {config.Srt.Port}");
        sb.AppendLine($"latency = {config.Srt.Latency}");
        if (!string.IsNullOrEmpty(config.Srt.Passphrase))
        {
            sb.AppendLine($"passphrase = {config.Srt.Passphrase}");
            sb.AppendLine($"pbKeyLen = {config.Srt.PbKeyLen}");
        }
        sb.AppendLine();

        sb.AppendLine("[logs]");
        sb.AppendLine($"level = {config.Log.Level}");
        sb.AppendLine($"directory = {config.Log.Directory}");
        sb.AppendLine($"maxSize = {config.Log.MaxSize}");
        sb.AppendLine($"rotation = {config.Log.Rotation}");
        sb.AppendLine();

        sb.AppendLine("[paths]");
        sb.AppendLine($"www = {config.Paths.Www}");
        sb.AppendLine($"data = {config.Paths.Data}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteProtocol(StringBuilder sb, string section, ProtocolSection proto, Action<StringBuilder>? extra = null)
    {
        sb.AppendLine($"[{section}]");
        sb.AppendLine($"enabled = {proto.Enabled.ToString().ToLower()}");
        sb.AppendLine($"host = {proto.Host}");
        sb.AppendLine($"port = {proto.Port}");
        extra?.Invoke(sb);
        sb.AppendLine();
    }

    private static void Apply(Dictionary<string, Dictionary<string, string>> raw, string section, Action<Dictionary<string, string>> action)
    {
        if (raw.TryGetValue(section, out var s))
            action(s);
    }

    private static string Get(Dictionary<string, string> s, string key, string fallback) =>
        s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    private static string? GetNullable(Dictionary<string, string> s, string key) =>
        s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static int GetInt(Dictionary<string, string> s, string key, int fallback) =>
        s.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    private static bool GetBool(Dictionary<string, string> s, string key, bool fallback) =>
        s.TryGetValue(key, out var v) ? v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" : fallback;
}
