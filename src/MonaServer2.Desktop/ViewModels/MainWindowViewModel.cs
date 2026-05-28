using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly StreamsViewModel _streams;
    private readonly SessionsViewModel _sessions;
    private readonly ConfigViewModel _config;
    private readonly LogsViewModel _logs;
    private readonly ServiceViewModel _service;
    private readonly PushStreamViewModel _pushStream;
    private readonly OBSSetupViewModel _obsSetup;
    private readonly SignalRService _signalR;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    [ObservableProperty]
    private string _connectionColor = "#DD4444";

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        StreamsViewModel streams,
        SessionsViewModel sessions,
        ConfigViewModel config,
        LogsViewModel logs,
        ServiceViewModel service,
        PushStreamViewModel pushStream,
        OBSSetupViewModel obsSetup,
        SignalRService signalR)
    {
        _dashboard = dashboard;
        _streams = streams;
        _sessions = sessions;
        _config = config;
        _logs = logs;
        _service = service;
        _pushStream = pushStream;
        _obsSetup  = obsSetup;
        _signalR = signalR;

        _currentPage = dashboard;

        _signalR.Connected += () => { IsConnected = true; ConnectionStatus = "Connected"; ConnectionColor = "#22DD88"; };
        _signalR.Disconnected += () => { IsConnected = false; ConnectionStatus = "Disconnected — retrying..."; ConnectionColor = "#DD4444"; };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _signalR.StartAsync();
        await _dashboard.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToDashboard()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _dashboard;
        await _dashboard.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToStreams()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _streams;
        await _streams.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToSessions()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _sessions;
        await _sessions.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToConfig()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _config;
        await _config.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToLogs()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _logs;
        await _logs.OnActivatedAsync();
    }

    private void DeactivatePushStreamIfActive()
    {
        if (CurrentPage is PushStreamViewModel ps) ps.OnDeactivated();
    }

    [RelayCommand]
    private async Task NavigateToService()
    {
        if (CurrentPage is PushStreamViewModel ps) ps.OnDeactivated();
        CurrentPage = _service;
        await _service.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToPushStream()
    {
        CurrentPage = _pushStream;
        await _pushStream.OnActivatedAsync();
    }

    [RelayCommand]
    private async Task NavigateToOBSSetup()
    {
        DeactivatePushStreamIfActive();
        CurrentPage = _obsSetup;
        await _obsSetup.OnActivatedAsync();
    }
}
