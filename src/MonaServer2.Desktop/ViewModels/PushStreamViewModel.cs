using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonaServer2.Core.Streaming;
using MonaServer2.Desktop.Services;

namespace MonaServer2.Desktop.ViewModels;

public partial class PushStreamViewModel : ViewModelBase
{
    private readonly ApiService _api;
    private CancellationTokenSource? _previewCts;

    // Source
    [ObservableProperty] private bool _isFileSource = true;
    [ObservableProperty] private bool _isCalibrationSource;
    [ObservableProperty] private string _filePath = "";

    // Destination
    [ObservableProperty] private string _rtmpUrl = "rtmp://localhost:1935/live";
    [ObservableProperty] private string _streamKey = "stream";

    // Encoding options
    [ObservableProperty] private int _videoBitrateKbps = 2500;
    [ObservableProperty] private int _width = 1280;
    [ObservableProperty] private int _height = 720;
    [ObservableProperty] private int _frameRate = 25;
    [ObservableProperty] private bool _loopFile = true;

    // Stream state
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _streamStatusText = "Not streaming";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    // Preview
    [ObservableProperty] private bool _isPreviewRunning;
    [ObservableProperty] private string _previewUrl = "";
    [ObservableProperty] private Bitmap? _previewFrame;
    [ObservableProperty] private string? _previewStatusText;

    // File picker injected by code-behind
    public Func<Task<string?>>? PickFileAsync { get; set; }

    public PushStreamViewModel(ApiService api, SignalRService signalR)
    {
        _api = api;

        signalR.OnStreamingStatusChanged += status =>
        {
            IsStreaming = status.IsStreaming;
            StreamStatusText = status.IsStreaming
                ? $"Streaming → {status.StreamKey}  ({status.SourceDescription})"
                : "Not streaming";
        };
    }

    public override async Task OnActivatedAsync()
    {
        var status = await _api.GetStreamStatusAsync();
        IsStreaming = status.IsStreaming;
        StreamStatusText = status.IsStreaming
            ? $"Streaming → {status.StreamKey}  ({status.SourceDescription})"
            : "Not streaming";

        // Suggest a preview URL derived from the RTMP destination
        UpdateSuggestedPreviewUrl();
    }

    partial void OnRtmpUrlChanged(string value) => UpdateSuggestedPreviewUrl();
    partial void OnStreamKeyChanged(string value) => UpdateSuggestedPreviewUrl();

    private void UpdateSuggestedPreviewUrl()
    {
        if (!string.IsNullOrWhiteSpace(PreviewUrl)) return;

        // Derive HTTP FLV preview URL from the RTMP push URL
        // rtmp://host:1935/live → http://host:80/live/<key>.flv
        try
        {
            var uri = new Uri(RtmpUrl);
            var httpPort = uri.Port == 1935 ? 80 : uri.Port;
            PreviewUrl = $"http://{uri.Host}:{httpPort}{uri.AbsolutePath.TrimEnd('/')}/{StreamKey}.flv";
        }
        catch
        {
            // malformed URL, leave it
        }
    }

    [RelayCommand]
    private void SelectFileSource()
    {
        IsFileSource = true;
        IsCalibrationSource = false;
    }

    [RelayCommand]
    private void SelectCalibrationSource()
    {
        IsFileSource = false;
        IsCalibrationSource = true;
    }

    [RelayCommand]
    private async Task BrowseFile()
    {
        if (PickFileAsync is null) return;
        var path = await PickFileAsync();
        if (path is not null) FilePath = path;
    }

    [RelayCommand]
    private async Task StartStream()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var req = new PushStreamRequest
            {
                SourceType = IsCalibrationSource ? StreamSourceType.Calibration : StreamSourceType.File,
                FilePath = IsFileSource ? FilePath : null,
                RtmpUrl = RtmpUrl,
                StreamKey = StreamKey,
                LoopFile = LoopFile,
                VideoBitrateKbps = VideoBitrateKbps,
                Width = Width,
                Height = Height,
                FrameRate = FrameRate,
            };

            await _api.StartStreamAsync(req);
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

    [RelayCommand]
    private async Task StopStream()
    {
        IsBusy = true;
        try
        {
            await _api.StopStreamAsync();
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

    [RelayCommand]
    private void StartPreview()
    {
        if (IsPreviewRunning || string.IsNullOrWhiteSpace(PreviewUrl)) return;

        _previewCts = new CancellationTokenSource();
        IsPreviewRunning = true;
        PreviewStatusText = "Connecting...";

        _ = PollPreviewAsync(_previewCts.Token);
    }

    [RelayCommand]
    private void StopPreview()
    {
        _previewCts?.Cancel();
        _previewCts = null;
        IsPreviewRunning = false;
        PreviewStatusText = null;
        PreviewFrame = null;
    }

    private async Task PollPreviewAsync(CancellationToken ct)
    {
        int failures = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var bytes = await _api.GetPreviewFrameAsync(PreviewUrl, ct: ct);
                if (bytes is not null)
                {
                    using var ms = new MemoryStream(bytes);
                    PreviewFrame = new Bitmap(ms);
                    PreviewStatusText = "Live";
                    failures = 0;
                }
                else
                {
                    failures++;
                    PreviewStatusText = failures < 5 ? "Waiting for stream..." : "No signal";
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                failures++;
                PreviewStatusText = "Preview unavailable";
            }

            await Task.Delay(1500, ct).ConfigureAwait(false);
        }

        IsPreviewRunning = false;
        PreviewStatusText = null;
    }

    // Called when navigating away — stop preview to free resources
    public void OnDeactivated()
    {
        _previewCts?.Cancel();
        _previewCts = null;
        IsPreviewRunning = false;
        PreviewFrame = null;
    }
}
