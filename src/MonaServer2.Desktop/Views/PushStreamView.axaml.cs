using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MonaServer2.Desktop.ViewModels;

namespace MonaServer2.Desktop.Views;

public partial class PushStreamView : UserControl
{
    public PushStreamView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not PushStreamViewModel vm) return;

        vm.PickFileAsync = async () =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select video file",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Video Files")
                    {
                        Patterns = ["*.mp4", "*.mkv", "*.avi", "*.mov", "*.ts", "*.flv", "*.wmv", "*.m4v"],
                    },
                    new FilePickerFileType("All Files") { Patterns = ["*"] },
                ],
            });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        };
    }
}
