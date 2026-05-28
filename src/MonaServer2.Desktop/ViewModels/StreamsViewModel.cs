using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Core.Models;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class StreamsViewModel : ViewModelBase
{
    private readonly ApiService _api;

    [ObservableProperty] private ObservableCollection<Publication> _publications = [];
    [ObservableProperty] private Publication? _selectedPublication;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _filterText = string.Empty;

    public StreamsViewModel(ApiService api, SignalRService signalR)
    {
        _api = api;
        signalR.OnPublicationsUpdated += pubs =>
        {
            Publications = new ObservableCollection<Publication>(
                string.IsNullOrWhiteSpace(FilterText)
                    ? pubs
                    : pubs.Where(p => p.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));
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
            var pubs = await _api.GetPublicationsAsync();
            Publications = new ObservableCollection<Publication>(pubs);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        _ = RefreshAsync();
    }
}
