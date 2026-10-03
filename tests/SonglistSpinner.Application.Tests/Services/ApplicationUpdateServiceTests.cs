using System.Net;
using System.Text;
using SonglistSpinner.Core.Updates;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class ApplicationUpdateServiceTests
{
    private static readonly Version RunningVersion = new(1, 1, 0);

    [Fact]
    public async Task Given_TheUserDismissedTheRelease_When_TheAppStartsAgain_Then_ItIsNotOfferedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var preferences = new InMemoryKeyValueStore();
        var firstRun = CreateService(new ReleaseFeed(Release("v1.2.0")), preferences);
        var offered = await firstRun.CheckForUpdateAsync(cancellationToken);
        firstRun.Dismiss(offered!);
        var nextRun = CreateService(new ReleaseFeed(Release("v1.2.0")), preferences);

        var update = await nextRun.CheckForUpdateAsync(cancellationToken);

        Assert.Null(update);
        // The key is a persisted contract: a dismissal saved by an installed version must keep working.
        Assert.Equal("v1.2.0", preferences.Values["dismissed_application_update"]);
    }

    [Fact]
    public async Task Given_AnOlderReleaseWasDismissed_When_ANewerOneIsPublished_Then_TheNewerOneIsOffered()
    {
        var preferences = new InMemoryKeyValueStore();
        preferences.SetValue("dismissed_application_update", "v1.2.0");
        var service = CreateService(new ReleaseFeed(Release("v1.3.0")), preferences);

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("v1.3.0", update?.Tag);
    }

    [Fact]
    public async Task Given_TheCheckFailed_When_CheckingAgain_Then_GitHubIsAskedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var feed = new ReleaseFeed(new HttpResponseMessage(HttpStatusCode.InternalServerError), Release("v1.2.0"));
        var service = CreateService(feed, new InMemoryKeyValueStore());
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CheckForUpdateAsync(cancellationToken));

        var update = await service.CheckForUpdateAsync(cancellationToken);

        Assert.Equal("v1.2.0", update?.Tag);
        Assert.Equal(2, feed.RequestCount);
    }

    [Fact]
    public async Task Given_TheCheckSucceeded_When_CheckingAgain_Then_GitHubIsNotAskedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var feed = new ReleaseFeed(Release("v1.2.0"));
        var service = CreateService(feed, new InMemoryKeyValueStore());
        await service.CheckForUpdateAsync(cancellationToken);

        var update = await service.CheckForUpdateAsync(cancellationToken);

        Assert.Equal("v1.2.0", update?.Tag);
        Assert.Equal(1, feed.RequestCount);
    }

    private static ApplicationUpdateService CreateService(ReleaseFeed feed, InMemoryKeyValueStore preferences) =>
        new(new GitHubReleaseUpdateChecker(new HttpClient(feed)), preferences, RunningVersion);

    private static HttpResponseMessage Release(string tag) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""
              {
                "tag_name": "{{tag}}",
                "html_url": "https://github.com/wowfood/songlistspinner-desktop/releases/tag/{{tag}}",
                "draft": false,
                "prerelease": false
              }
              """,
            Encoding.UTF8,
            "application/json")
    };

    /// <summary>GitHub's latest-release endpoint, answering each request with the next scripted response.</summary>
    private sealed class ReleaseFeed(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
