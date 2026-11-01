using System.Text.RegularExpressions;

namespace Exception.Uploader.Core;

/// <summary>
/// Finds the journal string in a character's saved variables file. The addon stores it on logout as
/// ExceptionJournalDB.upload = { data = "EXJ1:...", ... }. The guild portal's file drop uses the same rule. Base64 needs no escaping, so this needs no Lua parser.
/// </summary>
public static partial class SavedVariables
{
    public const string FileName = "Exception.lua";

    /// <summary>A saved variables file is a few MB at most</summary>
    public const long MaxFileBytes = 20_000_000;

    [GeneratedRegex("\\[\"upload\"\\]\\s*=\\s*\\{[^{}]*?\\[\"data\"\\]\\s*=\\s*\"(EXJ1:[A-Za-z0-9+/=]+)\"")]
    private static partial Regex UploadBlock();

    public static string? ExtractUpload(string savedVariables)
    {
        var match = UploadBlock().Match(savedVariables);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Reads while the game may be writing: a half-written file just has no match yet</summary>
    public static async Task<string?> ReadUploadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxFileBytes)
            return null;
        using var reader = new StreamReader(stream);
        return ExtractUpload(await reader.ReadToEndAsync(cancellationToken));
    }
}
