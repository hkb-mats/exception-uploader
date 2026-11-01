using System.Security.Cryptography;
using System.Text;

namespace Exception.Uploader.Core;

/// <summary>What the uploader last did for one character's file</summary>
public sealed record CharacterRecord(
    string Character,
    string? UploadedHash,
    DateTime? UploadedAt,
    string Message,
    bool Failed = false,
    int? Level = null);

/// <summary>
/// Decides which character files to upload. The game writes files in pieces, so a file waits until it
/// has been quiet for a moment, and a string already uploaded is never sent again.
/// </summary>
public sealed class UploadEngine
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)];

    private sealed class FileState
    {
        public DateTime SeenWriteTime;
        public DateTime? RetryAt;
        public int Failures;
    }

    private readonly IPortalClient _portal;
    private readonly TimeProvider _time;
    private readonly IUploadLog _log;
    private readonly Dictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterRecord> _records;

    public UploadEngine(IPortalClient portal, IDictionary<string, CharacterRecord> records, TimeProvider time, IUploadLog log)
    {
        _portal = portal;
        _records = new Dictionary<string, CharacterRecord>(records, StringComparer.Ordinal);
        _time = time;
        _log = log;
    }

    /// <summary>The token was refused: nothing uploads until it's replaced</summary>
    public bool TokenRefused { get; private set; }

    public IReadOnlyDictionary<string, CharacterRecord> Records => _records;

    public event Action? Changed;

    public void TokenReplaced()
    {
        TokenRefused = false;
        foreach (var state in _files.Values)
            state.SeenWriteTime = default;
        Changed?.Invoke();
    }

    /// <summary>Checks every file once. Called on a timer and whenever a file changes.</summary>
    public async Task ScanAsync(string wowFolder, string token, CancellationToken cancellationToken)
    {
        if (TokenRefused)
            return;

        foreach (var file in WowFolder.CharacterFiles(wowFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await ProcessAsync(file, token, cancellationToken))
                return;
        }
    }

    /// <summary>False when scanning should stop: the token was refused</summary>
    private async Task<bool> ProcessAsync(string file, string token, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        DateTime writeTime;
        try
        {
            writeTime = File.GetLastWriteTimeUtc(file);
        }
        catch (System.Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        if (!_files.TryGetValue(file, out var state))
            _files[file] = state = new FileState();

        var changed = writeTime != state.SeenWriteTime;
        var retryDue = state.RetryAt is { } retryAt && retryAt <= now;
        if ((!changed && !retryDue) || now - writeTime < QuietPeriod)
            return true;

        string? data;
        try
        {
            data = await SavedVariables.ReadUploadAsync(file, cancellationToken);
        }
        catch (System.Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true; // Still being written, or locked: the next scan sees it again
        }

        state.SeenWriteTime = writeTime;
        if (data is null)
            return true; // A character not played since the addon's update

        var hash = Hash(data);
        if (_records.TryGetValue(file, out var record) && record.UploadedHash == hash)
        {
            state.RetryAt = null;
            return true;
        }

        var character = WowFolder.CharacterName(file);
        var result = await _portal.UploadAsync(token, data, cancellationToken);
        _log.Write($"{character}: {result.Status}");

        switch (result.Status)
        {
            case UploadStatus.Uploaded:
                state.RetryAt = null;
                state.Failures = 0;
                Record(file, new CharacterRecord(character, hash, now, result.Message, Level: result.Level));
                return true;

            case UploadStatus.Rejected:
                state.RetryAt = null;
                state.Failures = 0;
                Record(file, Failed(record, character, result.Message));
                return true;

            case UploadStatus.Retry:
                state.RetryAt = now + RetryDelays[Math.Min(state.Failures, RetryDelays.Length - 1)];
                state.Failures++;
                Record(file, Failed(record, character, result.Message));
                return true;

            default:
                TokenRefused = true;
                Record(file, Failed(record, character, result.Message));
                return false;
        }
    }

    private static CharacterRecord Failed(CharacterRecord? previous, string character, string message) =>
        new(character, previous?.UploadedHash, previous?.UploadedAt, message, Failed: true, previous?.Level);

    private void Record(string file, CharacterRecord record)
    {
        _records[file] = record;
        Changed?.Invoke();
    }

    private static string Hash(string data) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
}
