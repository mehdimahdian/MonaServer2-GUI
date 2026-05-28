using CommunityToolkit.Mvvm.ComponentModel;
using MonaServer2.Core.Models;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private readonly SignalRService _signalR;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _uptime = "--";
    [ObservableProperty] private int _totalConnections;
    [ObservableProperty] private int _totalPublications;
    [ObservableProperty] private string _bandwidthIn = "0 B/s";
    [ObservableProperty] private string _bandwidthOut = "0 B/s";
    [ObservableProperty] private string _version = "--";
    [ObservableProperty] private string _statusBadge = "Stopped";
    [ObservableProperty] private string _statusColor = "#DD4444";
    [ObservableProperty] private string _statusBackground = "#3D1A1A";
    [ObservableProperty] private Dictionary<string, int> _connectionsByProtocol = [];

    public DashboardViewModel(ApiService api, SignalRService signalR)
    {
        _api = api;
        _signalR = signalR;
        _signalR.OnStatusChanged += UpdateFromStatus;
    }

    public override async Task OnActivatedAsync()
    {
        var status = await _api.GetStatusAsync();
        UpdateFromStatus(status);
    }

    private void UpdateFromStatus(ServerStatus status)
    {
        IsRunning = status.IsRunning;
        StatusBadge = status.IsRunning ? "Running" : "Stopped";
        StatusColor = status.IsRunning ? "#22DD88" : "#DD4444";
        StatusBackground = status.IsRunning ? "#1A3D2E" : "#3D1A1A";
        Version = status.Version ?? "--";
        TotalConnections = status.TotalConnections;
        TotalPublications = status.TotalPublications;
        BandwidthIn = FormatBytes(status.TotalByteRateIn);
        BandwidthOut = FormatBytes(status.TotalByteRateOut);
        ConnectionsByProtocol = status.ConnectionsByProtocol;

        Uptime = status.IsRunning
            ? FormatUptime(status.Uptime)
            : "--";
    }

    private static string FormatBytes(long bytesPerSec)
    {
        return bytesPerSec switch
        {
            >= 1_073_741_824 => $"{bytesPerSec / 1_073_741_824.0:F1} GB/s",
            >= 1_048_576     => $"{bytesPerSec / 1_048_576.0:F1} MB/s",
            >= 1024          => $"{bytesPerSec / 1024.0:F1} KB/s",
            _                => $"{bytesPerSec} B/s"
        };
    }

    private static string FormatUptime(TimeSpan t) =>
        t.TotalDays >= 1
            ? $"{(int)t.TotalDays}d {t.Hours:D2}h {t.Minutes:D2}m"
            : $"{t.Hours:D2}h {t.Minutes:D2}m {t.Seconds:D2}s";
}
