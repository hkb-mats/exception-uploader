using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Exception.Uploader.Core;

namespace Exception.Uploader;

public partial class MainWindow : Window
{
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#10b981"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#dc2626"));

    private readonly UploaderService _service;

    // For the XAML previewer and resource loader
    public MainWindow() : this(App.Service)
    {
    }

    public MainWindow(UploaderService service)
    {
        _service = service;
        InitializeComponent();

        StartAtLogin.IsChecked = service.Settings.StartAtLogin;
        service.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    /// <summary>Closing only hides the window: uploading carries on from the tray</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.CloseReason is WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Refresh()
    {
        var problem = _service.Problem;
        Status.Text = problem ?? "Watching for logouts";
        Status.Foreground = problem is null ? Good : Bad;

        var folder = _service.Settings.WowFolder;
        FolderText.Text = folder ?? "Not chosen yet";
        FolderHint.Text = folder is null
            ? "The folder with _forever_ or _retail_ in it, usually World of Warcraft."
            : WowFolder.CharacterFiles(folder).Count switch
            {
                0 => "No characters with the addon yet. Check the folder, or log out of WoW once.",
                1 => "1 character with the addon found",
                var count => $"{count} characters with the addon found"
            };

        var rows = _service.Records.OrderByDescending(r => r.UploadedAt).Select(CharacterRow).ToList();
        Characters.ItemsSource = rows;
        NoCharacters.IsVisible = rows.Count == 0;

        if (_service.NewerVersion is { } version)
        {
            UpdateButton.Content = $"Version {version} is available: download it";
            UpdateButton.IsVisible = true;
        }
    }

    private static Control CharacterRow(CharacterRecord record)
    {
        var uploaded = record.UploadedAt is { } at ? $"Uploaded {at.ToLocalTime():g}" : "Not uploaded yet";
        var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new(0, 0, 0, 6) };
        row.Children.Add(new TextBlock { Text = record.Character });
        row.Children.Add(new TextBlock
        {
            Text = record.Failed ? record.Message : uploaded,
            Foreground = record.Failed ? Bad : null,
            Opacity = record.Failed ? 1 : 0.75,
            TextWrapping = TextWrapping.Wrap
        });
        return row;
    }

    private void OnSaveToken(object? sender, RoutedEventArgs e)
    {
        var token = TokenBox.Text?.Trim();
        if (string.IsNullOrEmpty(token))
            return;
        if (!token.StartsWith("exu_", StringComparison.Ordinal))
        {
            Status.Text = "That isn't an upload token. They start with exu_.";
            Status.Foreground = Bad;
            return;
        }

        _service.SetToken(token);
        TokenBox.Text = string.Empty;
    }

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose your World of Warcraft folder",
            AllowMultiple = false
        });
        if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path)
            _service.SetWowFolder(path);
    }

    private void OnStartAtLoginClicked(object? sender, RoutedEventArgs e) =>
        _service.SetStartAtLogin(StartAtLogin.IsChecked == true);

    private void OnOpenPortal(object? sender, RoutedEventArgs e) => OpenBrowser(BuildInfo.PortalUrl);

    private void OnOpenDownload(object? sender, RoutedEventArgs e) => OpenBrowser(BuildInfo.PortalUrl);

    private static void OpenBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
