using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Exception.Uploader.Core;

namespace Exception.Uploader;

internal static class Program
{
    private static readonly TimeSpan NoDisplayRetry = TimeSpan.FromMinutes(5);

    [STAThread]
    public static int Main(string[] args)
    {
        var store = new UploaderStore(UploaderStore.DefaultFolder());
        using var instance = SingleInstance.TryAcquire(store.Folder);
        if (instance is null)
        {
            SingleInstance.RequestShow(store.Folder);
            return 0;
        }

        // Uploading needs no window, and it starts first: the window needs a display that's awake, and
        // this runs at login and overnight
        using var service = new UploaderService(store);
        service.Start();
        App.Service = service;

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        catch (InvalidOperationException e)
        {
            service.Log($"No window: {e.Message}");
            Thread.Sleep(NoDisplayRetry);
            instance.Dispose();
            if (Environment.ProcessPath is { } path)
                Process.Start(path, LoginItem.BackgroundArgument);
            return 0;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
