using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Core.Models;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class LogsViewModel : ViewModelBase
{
    private const int MaxEntries = 2000;

    [ObservableProperty] private ObservableCollection<LogEntry> _entries = [];
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private LogLevel _minLevel = LogLevel.Info;
    [ObservableProperty] private ObservableCollection<LogEntry> _filteredEntries = [];

    public List<LogLevel> LogLevels { get; } = Enum.GetValues<LogLevel>().ToList();

    public LogsViewModel(SignalRService signalR)
    {
        signalR.OnLogReceived += entry =>
        {
            if (Entries.Count >= MaxEntries)
                Entries.RemoveAt(0);

            Entries.Add(entry);
            ApplyFilter();
        };
    }

    public override Task OnActivatedAsync()
    {
        ApplyFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
        FilteredEntries.Clear();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnMinLevelChanged(LogLevel value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = Entries
            .Where(e => e.Level >= MinLevel)
            .Where(e => string.IsNullOrWhiteSpace(FilterText) ||
                        e.Message.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                        (e.Source?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));

        FilteredEntries = new ObservableCollection<LogEntry>(filtered);
    }
}
