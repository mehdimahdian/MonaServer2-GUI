namespace MonaServer2.Core.Update;

public class UpdateSettings
{
    public string GitHubRepo { get; set; } = "MonaSolutions/MonaServer2";
    public string InstallDirectory { get; set; } = "";
    public string WindowsAssetPattern { get; set; } = "windows";
    public string LinuxAssetPattern { get; set; } = "linux";
    public string MacOsAssetPattern { get; set; } = "macos";
}
