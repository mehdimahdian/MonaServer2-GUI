using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class ServiceViewModel : ViewModelBase
{
    private readonly ApiService _api;

    // Process state
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Unknown";
    [ObservableProperty] private string _statusColor = "#DD4444";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    // Update state
    [ObservableProperty] private string _installedVersion = "unknown";
    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private string? _pendingDownloadUrl;
    [ObservableProperty] private string? _checkResultMessage;
    [ObservableProperty] private int _updateProgress;
    [ObservableProperty] private string? _updateStatusMessage;
    [ObservableProperty] private bool _updateComplete;

    // Custom URL install
    [ObservableProperty] private bool _showCustomUrl;
    [ObservableProperty] private string _customUrl = "";
    [ObservableProperty] private string _customVersion = "";

    public ServiceViewModel(ApiService api, SignalRService signalR)
    {
        _api = api;

        signalR.OnStatusChanged += status =>
        {
            IsRunning = status.IsRunning;
            StatusText = status.IsRunning ? $"Running (PID {status.ProcessId})" : "Stopped";
            StatusColor = status.IsRunning ? "#22DD88" : "#DD4444";
        };

        signalR.OnUpdateProgress += progress =>
        {
            IsUpdating = progress.Phase is not ("done" or "error");
            UpdateProgress = progress.PercentComplete;
            UpdateStatusMessage = progress.Message;

            if (progress.Phase == "done")
            {
                UpdateComplete = true;
                // Refresh installed version from service
                _ = RefreshInstalledVersionAsync();
            }
            else if (progress.Phase == "error")
            {
                UpdateComplete = false;
                ErrorMessage = progress.Message;
            }
        };
    }

    public override async Task OnActivatedAsync()
    {
        var statusTask = _api.GetStatusAsync();
        var versionTask = _api.GetInstalledVersionAsync();
        await Task.WhenAll(statusTask, versionTask);

        var status = statusTask.Result;
        IsRunning = status.IsRunning;
        StatusText = status.IsRunning ? $"Running (PID {status.ProcessId})" : "Stopped";
        StatusColor = status.IsRunning ? "#22DD88" : "#DD4444";
        InstalledVersion = versionTask.Result;
    }

    [RelayCommand]
    private async Task Start() => await RunCommand(() => _api.StartProcessAsync());

    [RelayCommand]
    private async Task Stop() => await RunCommand(() => _api.StopProcessAsync());

    [RelayCommand]
    private async Task Restart() => await RunCommand(() => _api.RestartProcessAsync());

    [RelayCommand]
    private async Task CheckUpdate()
    {
        IsCheckingUpdate = true;
        CheckResultMessage = null;
        UpdateAvailable = false;
        LatestVersion = null;
        PendingDownloadUrl = null;
        UpdateComplete = false;
        ErrorMessage = null;

        try
        {
            var info = await _api.CheckUpdateAsync();
            InstalledVersion = info.InstalledVersion;
            LatestVersion = info.LatestVersion;
            UpdateAvailable = info.UpdateAvailable;
            PendingDownloadUrl = info.DownloadUrl;

            if (info.LatestVersion is null)
                CheckResultMessage = "No releases found on GitHub. Use a custom URL below.";
            else if (info.DownloadUrl is null)
                CheckResultMessage = $"Release {info.LatestVersion} found but no binary asset for this platform. Use a custom URL below.";
            else if (!info.UpdateAvailable)
                CheckResultMessage = $"Already up to date ({info.LatestVersion}).";
        }
        catch (Exception ex)
        {
            CheckResultMessage = $"Check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (string.IsNullOrWhiteSpace(PendingDownloadUrl)) return;
        await RunInstall(PendingDownloadUrl, LatestVersion ?? "unknown");
    }

    [RelayCommand]
    private async Task InstallFromUrl()
    {
        if (string.IsNullOrWhiteSpace(CustomUrl))
        {
            ErrorMessage = "Please enter a download URL.";
            return;
        }
        await RunInstall(CustomUrl, string.IsNullOrWhiteSpace(CustomVersion) ? "custom" : CustomVersion);
    }

    [RelayCommand]
    private void ToggleCustomUrl() => ShowCustomUrl = !ShowCustomUrl;

    private async Task RunInstall(string url, string version)
    {
        IsUpdating = true;
        UpdateComplete = false;
        UpdateProgress = 0;
        UpdateStatusMessage = "Preparing...";
        ErrorMessage = null;

        try
        {
            await _api.InstallUpdateAsync(url, version);
            // Actual progress arrives via SignalR — RunInstall returns immediately (202 Accepted)
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshInstalledVersionAsync()
    {
        InstalledVersion = await _api.GetInstalledVersionAsync();
    }

    private async Task RunCommand(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
