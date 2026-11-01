using Exception.Uploader.Core;
using FluentAssertions;

namespace Exception.Uploader.Tests;

public sealed class WowFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("wow").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AddFile(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void FindsCharactersInEveryClientFolder()
    {
        var forever = AddFile("_forever_/WTF/Account/ACC/Realm/Krise Stormborn/SavedVariables/Exception.lua");
        var era = AddFile("_classic_era_/WTF/Account/ACC/Realm/Alt/SavedVariables/Exception.lua");

        WowFolder.CharacterFiles(_root).Should().BeEquivalentTo([forever, era]);
    }

    [Fact]
    public void WorksWhenAClientFolderIsPicked()
    {
        var file = AddFile("_forever_/WTF/Account/ACC/Realm/Krise/SavedVariables/Exception.lua");

        WowFolder.CharacterFiles(Path.Combine(_root, "_forever_")).Should().Equal(file);
    }

    [Fact]
    public void LeavesOutOtherFiles()
    {
        AddFile("_forever_/WTF/Account/ACC/SavedVariables/Exception.lua");
        AddFile("_forever_/WTF/Account/ACC/Realm/Krise/SavedVariables/Exception.lua.bak");
        AddFile("_forever_/WTF/Account/ACC/Realm/Krise/SavedVariables/Other.lua");

        WowFolder.CharacterFiles(_root).Should().BeEmpty();
    }

    [Fact]
    public void NamesTheCharacterFromThePath() =>
        WowFolder.CharacterName(Path.Combine("a", "Realm", "Krise Stormborn", "SavedVariables", "Exception.lua"))
            .Should().Be("Krise Stormborn");

    [Fact]
    public void AMissingFolderHasNoCharacters() =>
        WowFolder.CharacterFiles(Path.Combine(_root, "gone")).Should().BeEmpty();
}
