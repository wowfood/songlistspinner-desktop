using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Core.StreamerSongList.Api.V2;
using SonglistSpinner.Core.Winner;
using SonglistSpinner.Services;
using Xunit;
using static SonglistSpinner.Application.Tests.ScriptedStreamerSongListClient;

namespace SonglistSpinner.Application.Tests.Services;

public class WinnerActionServiceTests
{
    [Fact]
    public async Task Given_RevealedWinner_When_MarkingItPlayed_Then_ThatQueueEntryIsMarkedPlayed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        using var winnerActions = new WinnerActionService(api, new NowPlayingTransitionService(api), session);

        await winnerActions.MarkPlayedAsync(42, cancellationToken);

        Assert.Equal([42], api.MarkedPlayed);
    }

    [Fact]
    public async Task Given_TheApiRejectsTheChange_When_MarkingTheWinnerPlayed_Then_TheFailureIsLoggedAndRethrownForTheDashboard()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rejection = new StreamerSongListApiException("StreamerSongList returned HTTP 500 (Internal Server Error).");
        var api = new ScriptedStreamerSongListClient { MarkPlayedFailure = rejection };
        var logger = new FakeLogger<WinnerActionService>();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        using var winnerActions = new WinnerActionService(api, new NowPlayingTransitionService(api), session, logger);

        var failure = await Assert.ThrowsAsync<StreamerSongListApiException>(
            () => winnerActions.MarkPlayedAsync(42, cancellationToken));

        Assert.Same(rejection, failure);
        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Marking winning queue entry 42 as played failed", entry.Message);
        Assert.Same(rejection, entry.Exception);
    }

    [Fact]
    public async Task Given_NoChannelLoaded_When_PromotingTheWinner_Then_FailsAskingForAReload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        using var winnerActions = new WinnerActionService(api, new NowPlayingTransitionService(api), session);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => winnerActions.PromoteToNowPlayingAsync(42, cancellationToken));

        Assert.Equal("The current streamer ID is unavailable. Reload the streamer and try again.", failure.Message);
        Assert.Empty(api.PromotedToNowPlaying);
    }

    [Fact]
    public async Task Given_ChannelLoadedWithASongPlaying_When_PromotingTheWinner_Then_CurrentSongIsCompletedAndWinnerPromoted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedStreamerSongListClient();
        var queueWithSongPlaying = new SpinnerQueueSnapshot { Items = [Song(42)], Playing = Song(40) };
        api.QueueResponses.Enqueue(_ => Task.FromResult(queueWithSongPlaying));
        api.QueueResponses.Enqueue(_ => Task.FromResult(QueueWith(42)));
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        await session.StartAsync(7, "wowfood", new SpinnerConfig(), [Song(42)], [], Song(40), cancellationToken);
        using var winnerActions = new WinnerActionService(api, new NowPlayingTransitionService(api), session);

        await winnerActions.PromoteToNowPlayingAsync(42, cancellationToken);

        Assert.Equal([7], api.NowPlayingMarkedPlayedFor);
        Assert.Equal([42], api.PromotedToNowPlaying);
    }
}
