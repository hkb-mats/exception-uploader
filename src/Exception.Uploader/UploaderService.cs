using Exception.Uploader.Core;

namespace Exception.Uploader;

/// <summary>
/// Keeps the engine running: a scan every 15 seconds, sooner when a file changes, and a daily look for
/// a newer version. The file watcher only wakes the scan early, so a missed event costs seconds.
/// </summary>
public sealed class UploaderService : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(1);

    private readonly UploaderStore _store;
    private readonly UploadEngine _engine;
    private readonly FileUploadLog _log;
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly FileSystemWatcher _showRequests;
    private string? _token;

    public UploaderService(UploaderStore store)
    {
        _store = store;
        _log = new FileUploadLog(store.LogPath);
        Settings = store.LoadSettings();
        _token = store.LoadToken();
        _http = new HttpClient { BaseAddress = new Uri(BuildInfo.ApiUrl), Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"ExceptionUploader/{BuildInfo.Version}");
        _engine = new UploadEngine(new PortalClient(_http), store.LoadHistory(), TimeProvider.System, _log);
        _engine.Changed += OnEngineChanged;
        _showRequests = new FileSystemWatcher(store.Folder, SingleInstance.ShowRequestFile);
        _showRequests.Created += (_, _) => ShowRequested?.Invoke();
        _showRequests.Changed += (_, _) => ShowRequested?.Invoke();
    }

    public UploaderSettings Settings { get; private set; }
    public string? NewerVersion { get; private set; }
    public bool IsConfigured => _token is not null && Settings.WowFolder is not null;
    public IEnumerable<CharacterRecord> Records => _engine.Records.Values;

    public string? Problem =>
        _token is null ? "Paste your upload token to start"
        : Settings.WowFolder is null ? "Choose your World of Warcraft folder to start"
        : _engine.TokenRefused ? "The portal refused the token. Create a new one on your Profile page."
        : NeedsMoveToApplications ? "Uploading, but move this app to Applications so it can start with your Mac."
        : null;

    private static bool NeedsMoveToApplications =>
        OperatingSystem.IsMacOS() && !BuildInfo.IsLocalApi && Environment.ProcessPath is { } path
        && !LoginItem.IsInApplications(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public event Action? Changed;

    /// <summary>Another copy was opened: show the window here instead</summary>
    public event Action? ShowRequested;

    public void Log(string line) => _log.Write(line);

    public void Start()
    {
        _showRequests.EnableRaisingEvents = true;
        Watch();
        ApplyLoginItem();
        _ = Task.Run(ScanLoopAsync);
        _ = Task.Run(UpdateLoopAsync);
    }

    public void ScanNow() => _wake.Release();

    public void SetToken(string token)
    {
        _store.SaveToken(token);
        _token = token;
        _engine.TokenReplaced();
        _log.Write("Token replaced");
        ApplyLoginItem();
        ScanNow();
    }

    public void SetWowFolder(string folder)
    {
        Settings = Settings with { WowFolder = folder };
        _store.SaveSettings(Settings);
        Watch();
        ApplyLoginItem();
        Changed?.Invoke();
        ScanNow();
    }

    public void SetStartAtLogin(bool enabled)
    {
        Settings = Settings with { StartAtLogin = enabled };
        _store.SaveSettings(Settings);
        ApplyLoginItem();
    }

    /// <summary>Waits for a working setup, and leaves a local build pointed at a local API out of it</summary>
    private void ApplyLoginItem()
    {
        if (IsConfigured && !BuildInfo.IsLocalApi && !NeedsMoveToApplications && Environment.ProcessPath is { } path)
            LoginItem.Set(Settings.StartAtLogin, path);
    }

    private void Watch()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();

        if (Settings.WowFolder is null)
            return;
        foreach (var root in WowFolder.AccountRoots(Settings.WowFolder))
        {
            var watcher = new FileSystemWatcher(root, SavedVariables.FileName)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    /// <summary>Waits out the game's writes, then lets the scan look</summary>
    private void OnFileEvent(object sender, FileSystemEventArgs e) =>
        _ = Task.Delay(UploadEngine.QuietPeriod + TimeSpan.FromSeconds(1), _stopping.Token)
            .ContinueWith(_ => ScanNow(), TaskContinuationOptions.OnlyOnRanToCompletion);

    private void OnEngineChanged()
    {
        _store.SaveHistory(_engine.Records);
        Changed?.Invoke();
    }

    private async Task ScanLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            if (_token is { } token && Settings.WowFolder is { } folder)
            {
                try
                {
                    await _engine.ScanAsync(folder, token, _stopping.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (System.Exception e)
                {
                    _log.Write($"Scan failed: {e.GetType().Name}");
                }
            }

            try
            {
                await _wake.WaitAsync(ScanInterval, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task UpdateLoopAsync()
    {
        if (BuildInfo.DownloadUrl.Length == 0)
            return;

        using var timer = new PeriodicTimer(UpdateInterval);
        do
        {
            NewerVersion = await UpdateCheck.FindNewerAsync(_http, $"{BuildInfo.DownloadUrl.TrimEnd('/')}/uploader.json", BuildInfo.Version, _stopping.Token);
            if (NewerVersion is not null)
                Changed?.Invoke();
        }
        while (await WaitForNextCheck(timer));
    }

    private async Task<bool> WaitForNextCheck(PeriodicTimer timer)
    {
        try
        {
            return await timer.WaitForNextTickAsync(_stopping.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _showRequests.Dispose();
        _http.Dispose();
    }
}
