using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Desktop.Tests.Services;

/// <remarks>
/// The API token is kept in Windows secure storage under its key alone; a changed or prefixed key would lose every
/// user's saved token on upgrade.
/// </remarks>
public class MauiSecureStorageStoreTests
{
    private const string Key = "streamersonglist_api_token";

    [Fact]
    public async Task Given_ASecretInSecureStorage_When_Read_Then_ItIsReturned()
    {
        var secureStorage = new InMemorySecureStorage { Values = { [Key] = "saved-token" } };
        var store = new MauiSecureStorageStore(secureStorage);

        var secret = await store.GetAsync(Key);

        Assert.Equal("saved-token", secret);
    }

    [Fact]
    public async Task Given_ASecret_When_Set_Then_ItIsWrittenToSecureStorageUnderItsKey()
    {
        var secureStorage = new InMemorySecureStorage();
        var store = new MauiSecureStorageStore(secureStorage);

        await store.SetAsync(Key, "new-token");

        Assert.Equal(new Dictionary<string, string> { [Key] = "new-token" }, secureStorage.Values);
    }

    [Fact]
    public void Given_ASavedSecret_When_Removed_Then_OnlyThatSecretIsGone()
    {
        var secureStorage = new InMemorySecureStorage { Values = { [Key] = "saved-token", ["another"] = "kept" } };
        var store = new MauiSecureStorageStore(secureStorage);

        store.Remove(Key);

        Assert.Equal(new Dictionary<string, string> { ["another"] = "kept" }, secureStorage.Values);
    }

    private sealed class InMemorySecureStorage : ISecureStorage
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key) => Values.Remove(key);

        public void RemoveAll() => Values.Clear();
    }
}
