using Avalonia;
using MonaServer2.Desktop;

try
{
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal: {ex}");
    return 1;
}

static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
