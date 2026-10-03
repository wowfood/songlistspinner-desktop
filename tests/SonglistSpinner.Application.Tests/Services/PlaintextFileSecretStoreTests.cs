using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public sealed class PlaintextFileSecretStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "secrets.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Given_ASavedSecret_When_ANewStoreOpensTheFile_Then_ItReadsTheSecret()
    {
        await new PlaintextFileSecretStore(FilePath).SetAsync("streamersonglist_api_token", "simulator-token");

        var secret = await new PlaintextFileSecretStore(FilePath).GetAsync("streamersonglist_api_token");

        Assert.Equal("simulator-token", secret);
    }

    [Fact]
    public async Task Given_ARemovedSecret_When_ANewStoreOpensTheFile_Then_ItIsMissing()
    {
        var firstLaunch = new PlaintextFileSecretStore(FilePath);
        await firstLaunch.SetAsync("streamersonglist_api_token", "simulator-token");
        firstLaunch.Remove("streamersonglist_api_token");

        var secret = await new PlaintextFileSecretStore(FilePath).GetAsync("streamersonglist_api_token");

        Assert.Null(secret);
    }
}
