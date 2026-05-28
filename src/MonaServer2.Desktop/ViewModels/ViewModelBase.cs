using CommunityToolkit.Mvvm.ComponentModel;

namespace MonaServer2.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual Task OnActivatedAsync() => Task.CompletedTask;
}
