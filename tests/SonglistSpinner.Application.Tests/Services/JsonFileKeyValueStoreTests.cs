using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public sealed class JsonFileKeyValueStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Given_NoFile_When_Reading_Then_EveryKeyIsMissingAndNoFileIsCreated()
    {
        var store = new JsonFileKeyValueStore(FilePath);

        var value = store.GetValue("local_settings");

        Assert.Null(value);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Given_SavedValues_When_ANewStoreOpensTheFile_Then_ItReadsThem()
    {
        var firstLaunch = new JsonFileKeyValueStore(FilePath);
        firstLaunch.SetValue("local_settings", """{"displayNowPlaying":true}""");
        firstLaunch.SetValue("dismissed_application_update", "v1.3.0");

        var nextLaunch = new JsonFileKeyValueStore(FilePath);

        Assert.Equal("""{"displayNowPlaying":true}""", nextLaunch.GetValue("local_settings"));
        Assert.Equal("v1.3.0", nextLaunch.GetValue("dismissed_application_update"));
    }

    [Fact]
    public void Given_ARemovedValue_When_ANewStoreOpensTheFile_Then_ItStaysRemoved()
    {
        var firstLaunch = new JsonFileKeyValueStore(FilePath);
        firstLaunch.SetValue("local_settings", "{}");
        firstLaunch.SetValue("streamersonglist_api_token_kind", "Streamer");
        firstLaunch.Remove("streamersonglist_api_token_kind");

        var nextLaunch = new JsonFileKeyValueStore(FilePath);

        Assert.Null(nextLaunch.GetValue("streamersonglist_api_token_kind"));
        Assert.Equal("{}", nextLaunch.GetValue("local_settings"));
    }
}
