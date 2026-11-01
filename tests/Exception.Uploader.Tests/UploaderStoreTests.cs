using Exception.Uploader.Core;
using FluentAssertions;

namespace Exception.Uploader.Tests;

public sealed class UploaderStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("uploader").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void KeepsSettingsAndHistory()
    {
        var store = new UploaderStore(_folder);
        store.SaveSettings(new UploaderSettings("/wow", StartAtLogin: false));
        store.SaveHistory(new Dictionary<string, CharacterRecord> { ["a"] = new("Krise", "hash", null, "Uploaded") });

        store.LoadSettings().Should().Be(new UploaderSettings("/wow", false));
        store.LoadHistory()["a"].Character.Should().Be("Krise");
    }

    [Fact]
    public void KeepsTheTokenInItsOwnPrivateFile()
    {
        var store = new UploaderStore(_folder);
        store.SaveToken("exu_secret");

        store.LoadToken().Should().Be("exu_secret");
        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(Path.Combine(_folder, "token.dat")).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);

        store.SaveSettings(new UploaderSettings("/wow"));
        Directory.GetFiles(_folder).Where(f => !f.EndsWith("token.dat"))
            .Select(File.ReadAllText).Should().NotContain(text => text.Contains("exu_secret"));
    }

    [Fact]
    public void ADamagedFileStartsOver()
    {
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{ not json");

        new UploaderStore(_folder).LoadSettings().Should().Be(new UploaderSettings());
    }

    [Fact]
    public void FileLogAppends()
    {
        var path = Path.Combine(_folder, "uploader.log");
        var log = new FileUploadLog(path);

        log.Write("Krise: Uploaded");
        log.Write("Krise: Retry");

        File.ReadAllLines(path).Should().HaveCount(2);
    }

    [Fact]
    public void OnlyOneCopyRunsAtATime()
    {
        using (var first = SingleInstance.TryAcquire(_folder))
        {
            first.Should().NotBeNull();
            SingleInstance.TryAcquire(_folder).Should().BeNull();
        }

        using var afterwards = SingleInstance.TryAcquire(_folder);
        afterwards.Should().NotBeNull();
    }

    [Theory]
    [InlineData("/Applications/Exception Uploader.app/Contents/MacOS/ExceptionUploader", true)]
    [InlineData("/Users/krise/Applications/Exception Uploader.app/Contents/MacOS/ExceptionUploader", true)]
    [InlineData("/Users/krise/Downloads/Exception Uploader.app/Contents/MacOS/ExceptionUploader", false)]
    [InlineData("/Volumes/Exception Uploader/Exception Uploader.app/Contents/MacOS/ExceptionUploader", false)]
    [InlineData("/private/var/folders/xy/AppTranslocation/1/d/Exception Uploader.app/Contents/MacOS/ExceptionUploader", false)]
    public void OnlyApplicationsCountsAsInstalledOnAMac(string path, bool installed) =>
        LoginItem.IsInApplications(path, "/Users/krise").Should().Be(installed);

    [Fact]
    public void MacLoginItemStartsInTheBackgroundAndEscapesThePath()
    {
        var agent = Path.Combine(_folder, "agents", "uploader.plist");

        LoginItem.SetMac(true, "/Applications/Tom & Jerry.app/Contents/MacOS/Exception Uploader", agent);

        File.ReadAllText(agent).Should().Contain("Tom &amp; Jerry").And.Contain(LoginItem.BackgroundArgument);
        LoginItem.SetMac(false, "ignored", agent);
        File.Exists(agent).Should().BeFalse();
    }
}
