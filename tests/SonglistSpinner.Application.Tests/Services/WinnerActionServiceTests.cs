using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
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
