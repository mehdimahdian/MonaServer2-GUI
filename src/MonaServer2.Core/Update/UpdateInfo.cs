namespace MonaServer2.Core.Update;

public record UpdateInfo
{
    public string InstalledVersion { get; init; } = "unknown";
    public string? LatestVersion { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public long? AssetSizeBytes { get; init; }
}

public record UpdateProgress
{
    public string Phase { get; init; } = "";
    public int PercentComplete { get; init; }
    public string? Message { get; init; }
}
