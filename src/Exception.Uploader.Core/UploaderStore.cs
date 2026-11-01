using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Exception.Uploader.Core;

public sealed record UploaderSettings(string? WowFolder = null, bool StartAtLogin = true);

/// <summary>
/// The uploader's files in the user's app data folder. The token gets a file of its own, readable only
/// by this user, and encrypted to the user on Windows.
/// </summary>
public sealed class UploaderStore
{
    private readonly string _folder;

    public UploaderStore(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    /// <summary>EXCEPTION_UPLOADER_DATA keeps a local run away from a member's real setup</summary>
    public static string DefaultFolder() =>
        Environment.GetEnvironmentVariable("EXCEPTION_UPLOADER_DATA") is { Length: > 0 } folder
            ? folder
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
                "Exception Uploader");

    public string Folder => _folder;
    public string LogPath => Path.Combine(_folder, "uploader.log");
    private string SettingsPath => Path.Combine(_folder, "settings.json");
    private string HistoryPath => Path.Combine(_folder, "history.json");
    private string TokenPath => Path.Combine(_folder, "token.dat");

    public UploaderSettings LoadSettings() =>
        Read(SettingsPath, UploaderJson.Default.UploaderSettings) ?? new UploaderSettings();

    public void SaveSettings(UploaderSettings settings) =>
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, UploaderJson.Default.UploaderSettings));

    public Dictionary<string, CharacterRecord> LoadHistory() =>
        Read(HistoryPath, UploaderJson.Default.DictionaryStringCharacterRecord) ?? [];

    public void SaveHistory(IReadOnlyDictionary<string, CharacterRecord> records) =>
        File.WriteAllText(HistoryPath, JsonSerializer.Serialize(
            new Dictionary<string, CharacterRecord>(records), UploaderJson.Default.DictionaryStringCharacterRecord));

    public string? LoadToken()
    {
        if (!File.Exists(TokenPath))
            return null;
        try
        {
            var bytes = File.ReadAllBytes(TokenPath);
            return Encoding.UTF8.GetString(OperatingSystem.IsWindows() ? Unprotect(bytes) : bytes);
        }
        catch (System.Exception e) when (e is IOException or CryptographicException)
        {
            return null;
        }
    }

    public void SaveToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        if (OperatingSystem.IsWindows())
            bytes = Protect(bytes);

        // Created with its mode, so the token is never readable by others, even for a moment
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var stream = new FileStream(TokenPath, options);
        stream.Write(bytes);
    }

    public void DeleteToken() => File.Delete(TokenPath);

    private static T? Read<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), type);
        }
        catch (System.Exception e) when (e is JsonException or IOException)
        {
            return null; // A damaged file only means starting over: uploads are safe to repeat
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] bytes) =>
        ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] bytes) =>
        ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
}
