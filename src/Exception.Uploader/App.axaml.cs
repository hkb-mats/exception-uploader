using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Exception.Uploader.Core;

namespace Exception.Uploader;

public class App : Application
{
    /// <summary>Started before the UI, because uploading goes on without one</summary>
    public static UploaderService Service { get; set; } = null!;

    private MainWindow? _window;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            Service.ShowRequested += () => Dispatcher.UIThread.Post(ShowWindow);

            // Opening the app again on a Mac reaches this copy, not a new one
            if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            {
                activatable.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen)
                        ShowWindow();
                };
            }

            var background = desktop.Args?.Contains(LoginItem.BackgroundArgument) ?? false;
            if (!background || !Service.IsConfigured)
                ShowWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowWindow()
    {
        _window ??= new MainWindow(Service);
        _window.Show();
        _window.Activate();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowWindow();
    private void OnOpenClicked(object? sender, EventArgs e) => ShowWindow();
    private void OnUploadClicked(object? sender, EventArgs e) => Service.ScanNow();
    private void OnQuitClicked(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => _desktop?.Shutdown());
}
