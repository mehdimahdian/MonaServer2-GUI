namespace MonaServer2.Service;

public class MonaServerSettings
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "http://localhost:80";
    public string ApiAdminPath { get; set; } = "/admin/api";
    public int ApiTimeoutMs { get; set; } = 3000;
    public bool AutoStart { get; set; } = true;
    public bool AutoRestart { get; set; } = true;
    public int RestartDelayMs { get; set; } = 5000;
    public int PollIntervalMs { get; set; } = 2000;

    public string ResolvedExecutablePath =>
        string.IsNullOrWhiteSpace(ExecutablePath)
            ? FindBinary()
            : ExecutablePath;

    private static string FindBinary()
    {
        var name = OperatingSystem.IsWindows() ? "MonaServer.exe" : "MonaServer";

        // 1. tools/monaserver2/ next to service binary
        var toolsPath = Path.Combine(AppContext.BaseDirectory, "tools", "monaserver2", name);
        if (File.Exists(toolsPath)) return toolsPath;

        // 2. PATH
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        return name; // let the OS try
    }
}

public class ServiceConfiguration
{
    public int Port { get; set; } = 8080;
    public List<string> AllowedOrigins { get; set; } = [];
}
