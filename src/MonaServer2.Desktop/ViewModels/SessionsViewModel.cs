using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Core.Models;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class SessionsViewModel : ViewModelBase
{
    private readonly ApiService _api;

    [ObservableProperty] private ObservableCollection<Session> _sessions = [];
    [ObservableProperty] private Session? _selectedSession;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _protocolFilter = "All";

    public List<string> ProtocolOptions { get; } = ["All", "HTTP", "RTMP", "SRT", "WebSocket", "RTMFP"];

    public SessionsViewModel(ApiService api, SignalRService signalR)
    {
        _api = api;
        signalR.OnSessionsUpdated += sessions =>
        {
            var filtered = ProtocolFilter == "All"
                ? sessions
                : sessions.Where(s => s.Protocol.Equals(ProtocolFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            Sessions = new ObservableCollection<Session>(filtered);
        };
    }

    public override async Task OnActivatedAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _api.GetSessionsAsync();
            var filtered = ProtocolFilter == "All"
                ? all
                : all.Where(s => s.Protocol.Equals(ProtocolFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            Sessions = new ObservableCollection<Session>(filtered);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnProtocolFilterChanged(string value) => _ = RefreshAsync();
}
