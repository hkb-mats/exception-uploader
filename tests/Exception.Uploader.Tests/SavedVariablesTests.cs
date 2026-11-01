using Exception.Uploader.Core;
using FluentAssertions;

namespace Exception.Uploader.Tests;

public class SavedVariablesTests
{
    /// <summary>Written by the Exception addon's own test run, the way the game writes it</summary>
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void FindsTheStringInAFileTheAddonWrote()
    {
        var file = Fixture("saved-variables.lua");

        var data = SavedVariables.ExtractUpload(file);

        data.Should().StartWith("EXJ1:");
        file.Should().Contain($"\"{data}\"");
    }

    [Fact]
    public void FindsItWhateverOrderTheClientWroteTheKeysIn() =>
        SavedVariables.ExtractUpload("[\"upload\"] = {\n\t[\"exportedAt\"] = 1,\n\t[\"addon\"] = \"2.2.0\",\n\t[\"data\"] = \"EXJ1:abc=\",\n},\n")
            .Should().Be("EXJ1:abc=");

    [Fact]
    public void FindsNothingInAFileFromAnOlderAddon() =>
        SavedVariables.ExtractUpload("ExceptionJournalDB = {\n\t[\"journalKey\"] = \"abc\",\n}\n").Should().BeNull();

    [Fact]
    public void IgnoresANoteThatImitatesTheKey() =>
        SavedVariables.ExtractUpload("""["text"] = "[\"upload\"] = { [\"data\"] = \"EXJ1:fake\" }",""").Should().BeNull();

    [Fact]
    public void IgnoresAHalfWrittenString() =>
        SavedVariables.ExtractUpload("[\"upload\"] = {\n\t[\"data\"] = \"EXJ1:abc").Should().BeNull();
}
