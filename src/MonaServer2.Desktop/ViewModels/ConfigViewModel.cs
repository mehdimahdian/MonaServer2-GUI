using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Core.Config;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class ConfigViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private MonaServerConfig? _originalConfig;

    [ObservableProperty] private MonaServerConfig _config = new();
    [ObservableProperty] private string _rawIni = string.Empty;
    [ObservableProperty] private bool _isRawMode;
    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _statusIsError;

    public ConfigViewModel(ApiService api) => _api = api;

    public override async Task OnActivatedAsync() => await LoadAsync();

    [RelayCommand]
    private async Task Load() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        try
        {
            Config = await _api.GetConfigAsync();
            RawIni = await _api.GetConfigRawAsync();
            _originalConfig = Config;
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load config: {ex.Message}";
            StatusIsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        IsLoading = true;
        StatusMessage = null;
        try
        {
            if (IsRawMode)
                await _api.SaveConfigRawAsync(RawIni);
            else
                await _api.SaveConfigAsync(Config);

            HasUnsavedChanges = false;
            StatusMessage = "Configuration saved. Restart MonaServer2 for changes to take effect.";
            StatusIsError = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            StatusIsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRawMode = !IsRawMode;
    }
}
