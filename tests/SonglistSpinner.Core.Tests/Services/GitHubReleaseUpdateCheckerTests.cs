using System.Net;
using System.Text;
using SonglistSpinner.Core.Services;
using Xunit;

namespace SonglistSpinner.Core.Tests.Services;

public class GitHubReleaseUpdateCheckerTests
{
    [Fact]
    public async Task Given_NewerPublishedRelease_When_Checking_Then_ReturnsTrustedRelease()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://github.com/wowfood/songlistspinner-desktop/releases/tag/v1.2.0",
              "draft": false,
              "prerelease": false,
              "published_at": "2026-08-14T10:00:00Z"
            }
            """));
        var checker = new GitHubReleaseUpdateChecker(new HttpClient(handler));

        var update = await checker.CheckAsync(
            new Version(1, 1, 0),
            TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 2, 0), update.Version);
        Assert.Equal("v1.2.0", update.Tag);
        Assert.Equal(
            "https://github.com/wowfood/songlistspinner-desktop/releases/tag/v1.2.0",
            update.ReleaseUri.AbsoluteUri);
        Assert.Equal("application/vnd.github+json", handler.Accept);
        Assert.Equal("SonglistSpinner-Desktop/1.1.0", handler.UserAgent);
        Assert.Equal("2026-03-10", handler.ApiVersion);
    }

    [Theory]
    [InlineData("v1.1.0")]
    [InlineData("v1.0.9")]
    public async Task Given_NonNewerRelease_When_Checking_Then_ReturnsNoUpdate(string tag)
    {
        var handler = new RecordingHandler(_ => JsonResponse(ReleaseJson(tag)));
        var checker = new GitHubReleaseUpdateChecker(new HttpClient(handler));

        var update = await checker.CheckAsync(
            new Version(1, 1, 0),
            TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    [Fact]
    public async Task Given_NoPublishedRelease_When_Checking_Then_ReturnsNoUpdate()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var checker = new GitHubReleaseUpdateChecker(new HttpClient(handler));

        var update = await checker.CheckAsync(
            new Version(1, 1, 0),
            TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Given_UnpublishedRelease_When_Checking_Then_ReturnsNoUpdate(
        bool draft,
        bool prerelease)
    {
        var handler = new RecordingHandler(_ => JsonResponse(ReleaseJson(
            "v1.2.0",
            draft: draft,
            prerelease: prerelease)));
        var checker = new GitHubReleaseUpdateChecker(new HttpClient(handler));

        var update = await checker.CheckAsync(
            new Version(1, 1, 0),
            TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    [Theory]
    [InlineData("release-1.2.0", "https://github.com/wowfood/songlistspinner-desktop/releases/tag/release-1.2.0")]
    [InlineData("v1.2.0", "https://example.com/wowfood/songlistspinner-desktop/releases/tag/v1.2.0")]
    public async Task Given_InvalidReleaseMetadata_When_Checking_Then_ReturnsNoUpdate(
        string tag,
        string releaseUrl)
    {
        var handler = new RecordingHandler(_ => JsonResponse(ReleaseJson(tag, releaseUrl)));
        var checker = new GitHubReleaseUpdateChecker(new HttpClient(handler));

        var update = await checker.CheckAsync(
            new Version(1, 1, 0),
            TestContext.Current.CancellationToken);

        Assert.Null(update);
    }

    private static string ReleaseJson(
        string tag,
        string releaseUrl = "https://github.com/wowfood/songlistspinner-desktop/releases/tag/v1.2.0",
        bool draft = false,
        bool prerelease = false)
    {
        return $$"""
                 {
                   "tag_name": "{{tag}}",
                   "html_url": "{{releaseUrl}}",
                   "draft": {{draft.ToString().ToLowerInvariant()}},
                   "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
                   "published_at": "2026-08-14T10:00:00Z"
                 }
                 """;
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public string? Accept { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Accept = request.Headers.Accept.Single().MediaType;
            ApiVersion = request.Headers.GetValues("X-GitHub-Api-Version").Single();
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(responseFactory(request));
        }
    }
}
