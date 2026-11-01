namespace Exception.Uploader.Core;

/// <summary>
/// Finds the addon's per-character files under the folder a member picked. Clients live in folders like
/// _retail_ or _classic_era_ under World of Warcraft, and Forever's name isn't known until launch, so
/// any folder with WTF/Account counts. Only ever lists these paths.
/// </summary>
public static class WowFolder
{
    /// <summary>The WTF/Account folders for what was picked: a client folder or World of Warcraft itself</summary>
    public static IReadOnlyList<string> AccountRoots(string picked)
    {
        if (!Directory.Exists(picked))
            return [];

        var candidates = new List<string> { picked };
        try
        {
            candidates.AddRange(Directory.EnumerateDirectories(picked));
        }
        catch (System.Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An unreadable child isn't a reason to give up on the folder itself
        }
        return candidates
            .Select(folder => Path.Combine(folder, "WTF", "Account"))
            .Where(Directory.Exists)
            .ToList();
    }

    /// <summary>WTF/Account/account/realm/character/SavedVariables/Exception.lua</summary>
    public static IReadOnlyList<string> CharacterFiles(string picked)
    {
        var files = new List<string>();
        foreach (var root in AccountRoots(picked))
        {
            foreach (var account in Subfolders(root))
            foreach (var realm in Subfolders(account))
            foreach (var character in Subfolders(realm))
            {
                var file = Path.Combine(character, "SavedVariables", SavedVariables.FileName);
                if (File.Exists(file))
                    files.Add(file);
            }
        }
        return files;
    }

    /// <summary>The character a file belongs to, for display: the export itself says who it really is</summary>
    public static string CharacterName(string file) =>
        Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file))) ?? file;

    private static IEnumerable<string> Subfolders(string folder)
    {
        try
        {
            return Directory.EnumerateDirectories(folder).ToList();
        }
        catch (System.Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
