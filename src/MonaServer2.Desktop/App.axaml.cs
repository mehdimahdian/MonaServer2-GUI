using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MonaServer2.Desktop.Services;
using MonaServer2.Desktop.ViewModels;
using MonaServer2.Desktop.Views;

namespace MonaServer2.Desktop;

public class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        // App settings (read from env or defaults)
        var serviceUrl = Environment.GetEnvironmentVariable("MONASERVER2_GUI_URL") ?? "http://localhost:8080";

        sc.AddHttpClient<ApiService>(c => c.BaseAddress = new Uri(serviceUrl));
        sc.AddSingleton<SignalRService>(sp => new SignalRService(serviceUrl));

        // ViewModels — all singletons so navigation preserves state
        sc.AddSingleton<MainWindowViewModel>();
        sc.AddSingleton<DashboardViewModel>();
        sc.AddSingleton<StreamsViewModel>();
        sc.AddSingleton<SessionsViewModel>();
        sc.AddSingleton<ConfigViewModel>();
        sc.AddSingleton<LogsViewModel>();
        sc.AddSingleton<ServiceViewModel>();
        sc.AddSingleton<PushStreamViewModel>();
        sc.AddSingleton<OBSSetupViewModel>();

        return sc.BuildServiceProvider();
    }
}
