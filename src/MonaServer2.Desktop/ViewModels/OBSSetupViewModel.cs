using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Desktop.Services;
using System.Collections.ObjectModel;

namespace MonaServer2.Desktop.ViewModels;

public partial class OBSSetupViewModel : ViewModelBase
{
    private readonly ApiService _api;

    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _obsFound;
    [ObservableProperty] private bool _pluginInstalled;
    [ObservableProperty] private string _obsStatusText = "Not checked";
    [ObservableProperty] private string _pluginStatusText = "Not checked";
    [ObservableProperty] private string _obsStatusColor = "#484860";
    [ObservableProperty] private string _pluginStatusColor = "#484860";
    [ObservableProperty] private string? _obsInstallPath;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    // Manual install instructions
    [ObservableProperty] private bool _showManualInstructions;
    [ObservableProperty] private string _downloadUrl = "";
    [ObservableProperty] private ObservableCollection<string> _installSteps = [];

    public OBSSetupViewModel(ApiService api)
    {
        _api = api;
    }

    public override async Task OnActivatedAsync() => await DetectOBS();

    [RelayCommand]
    private async Task DetectOBS()
    {
        IsDetecting = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var info = await _api.DetectOBSAsync();

            ObsFound = info.IsInstalled;
            ObsInstallPath = info.InstallPath;
            PluginInstalled = info.IsPluginInstalled;

            if (info.IsInstalled)
            {
                ObsStatusText  = $"Found: {info.InstallPath}";
                ObsStatusColor = "#22DD88";
            }
            else
            {
                ObsStatusText  = "OBS Studio not detected";
                ObsStatusColor = "#DDAA22";
            }

            if (info.IsPluginInstalled)
            {
                PluginStatusText  = info.PluginVersion is not null
                    ? $"Installed (v{info.PluginVersion})"
                    : "Installed";
                PluginStatusColor = "#22DD88";
            }
            else
            {
                PluginStatusText  = "Not installed";
                PluginStatusColor = info.IsInstalled ? "#DD4444" : "#484860";
            }

            // Load manual instructions
            var inst = await _api.GetOBSManualInstructionsAsync();
            if (inst is not null)
            {
                DownloadUrl = inst.DownloadUrl;
                InstallSteps.Clear();
                foreach (var step in inst.Steps ?? [])
                    InstallSteps.Add(step);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsDetecting = false;
        }
    }

    [RelayCommand]
    private async Task InstallPlugin()
    {
        IsInstalling = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var result = await _api.InstallOBSPluginAsync(ObsInstallPath);
            if (result.Success)
            {
                SuccessMessage = $"Plugin installed successfully to:\n{result.InstalledPath}\n\nRestart OBS Studio to activate it.";
                PluginInstalled   = true;
                PluginStatusText  = "Installed";
                PluginStatusColor = "#22DD88";
            }
            else
            {
                ErrorMessage = result.Error ?? "Installation failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void ToggleManualInstructions() =>
        ShowManualInstructions = !ShowManualInstructions;
}
