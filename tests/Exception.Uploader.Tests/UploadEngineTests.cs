using Exception.Uploader.Core;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Exception.Uploader.Tests;

public sealed class UploadEngineTests : IDisposable
{
    private const string Token = "exu_secret-token";

    private sealed class FakePortal : IPortalClient
    {
        public List<string> Uploads { get; } = [];
        public Queue<UploadResult> Results { get; } = new();

        public Task<UploadResult> UploadAsync(string token, string data, CancellationToken cancellationToken)
        {
            token.Should().Be(Token);
            Uploads.Add(data);
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new UploadResult(UploadStatus.Uploaded, "Uploaded", 12));
        }
    }

    private sealed class MemoryLog : IUploadLog
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private readonly string _root = Directory.CreateTempSubdirectory("wow").FullName;
    private readonly FakePortal _portal = new();
    private readonly MemoryLog _log = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly UploadEngine _engine;
    private readonly string _file;

    public UploadEngineTests()
    {
        _engine = new UploadEngine(_portal, new Dictionary<string, CharacterRecord>(), _time, _log);
        _file = Path.Combine(_root, "_forever_/WTF/Account/ACC/Realm/Krise/SavedVariables/Exception.lua");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Logout(string data)
    {
        File.WriteAllText(_file, $"ExceptionJournalDB = {{\n\t[\"upload\"] = {{\n\t\t[\"data\"] = \"{data}\",\n\t}},\n}}\n");
        File.SetLastWriteTimeUtc(_file, _time.GetUtcNow().UtcDateTime);
    }

    private void Wait(TimeSpan time) => _time.Advance(time);
    private Task ScanAsync() => _engine.ScanAsync(_root, Token, CancellationToken.None);

    [Fact]
    public async Task UploadsAfterTheFileGoesQuiet()
    {
        Logout("EXJ1:one");

        await ScanAsync();
        _portal.Uploads.Should().BeEmpty("the game may still be writing");

        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();
        _portal.Uploads.Should().Equal("EXJ1:one");
        _engine.Records[_file].Should().Match<CharacterRecord>(r => r.Character == "Krise" && !r.Failed && r.Level == 12);
    }

    [Fact]
    public async Task ADifferentStringUploadsAndTheSameOneDoesNot()
    {
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod + TimeSpan.FromSeconds(1));
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(1);

        Logout("EXJ1:two");
        Wait(UploadEngine.QuietPeriod + TimeSpan.FromSeconds(1));
        await ScanAsync();
        _portal.Uploads.Should().Equal("EXJ1:one", "EXJ1:two");
    }

    [Fact]
    public async Task HistoryStopsARepeatAfterARestart()
    {
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        var restarted = new UploadEngine(_portal, _engine.Records.ToDictionary(), _time, _log);
        await restarted.ScanAsync(_root, Token, CancellationToken.None);

        _portal.Uploads.Should().HaveCount(1);
    }

    [Fact]
    public async Task ACharacterWithoutAStringIsLeftAlone()
    {
        File.WriteAllText(_file, "ExceptionJournalDB = {}");
        Wait(UploadEngine.QuietPeriod);

        await ScanAsync();

        _portal.Uploads.Should().BeEmpty();
        _engine.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task ARejectedExportWaitsForTheFileToChange()
    {
        _portal.Results.Enqueue(new UploadResult(UploadStatus.Rejected, "Add Krise on the portal"));
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        Wait(TimeSpan.FromDays(1));
        await ScanAsync();

        _portal.Uploads.Should().HaveCount(1);
        _engine.Records[_file].Should().Match<CharacterRecord>(r => r.Failed && r.Message == "Add Krise on the portal");
    }

    [Fact]
    public async Task AnOutageRetriesWithBackoffUntilItWorks()
    {
        _portal.Results.Enqueue(new UploadResult(UploadStatus.Retry, "Can't reach the portal"));
        _portal.Results.Enqueue(new UploadResult(UploadStatus.Retry, "Can't reach the portal"));
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        await ScanAsync();
        _portal.Uploads.Should().HaveCount(1, "not before the delay");

        Wait(TimeSpan.FromSeconds(30));
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(2);

        Wait(TimeSpan.FromSeconds(30));
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(2, "the second delay is longer");

        Wait(TimeSpan.FromMinutes(2));
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(3);
        _engine.Records[_file].Failed.Should().BeFalse();
    }

    [Fact]
    public async Task ARefusedTokenStopsEverythingUntilItsReplaced()
    {
        _portal.Results.Enqueue(new UploadResult(UploadStatus.TokenRefused, "The portal refused the token"));
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        _engine.TokenRefused.Should().BeTrue();
        Wait(TimeSpan.FromHours(1));
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(1);

        _engine.TokenReplaced();
        await ScanAsync();
        _portal.Uploads.Should().HaveCount(2);
    }

    [Fact]
    public async Task TheLogNeverHoldsTheTokenOrTheExport()
    {
        Logout("EXJ1:one");
        Wait(UploadEngine.QuietPeriod);
        await ScanAsync();

        _log.Lines.Should().ContainSingle().Which.Should().Be("Krise: Uploaded");
    }
}
