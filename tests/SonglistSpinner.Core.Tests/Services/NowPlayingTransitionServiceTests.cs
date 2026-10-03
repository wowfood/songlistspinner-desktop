using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using Xunit;

namespace SonglistSpinner.Core.Tests.Services;

public class NowPlayingTransitionServiceTests
{
    [Fact]
    public async Task Given_NoCurrentSong_When_PromotingWinner_Then_PromotesWinnerDirectly()
    {
        var api = new RecordingStreamerSongListClient(new SpinnerQueueSnapshot { Items = [new() { QueueId = 91 }] });
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            new StreamerId(314),
            new QueueEntryId(91),
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "promote:91"], api.Calls);
    }

    [Fact]
    public async Task Given_CurrentSong_When_PromotingWinner_Then_CompletesCurrentBeforePromotion()
    {
        var api = new RecordingStreamerSongListClient(
            Snapshot(77),
            Snapshot(42));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            new StreamerId(314),
            new QueueEntryId(91),
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "complete:314", "fetch", "promote:91"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerWasAutoPromoted_When_CompletingCurrent_Then_DoesNotPromoteTwice()
    {
        var api = new RecordingStreamerSongListClient(
            Snapshot(77),
            Snapshot(91));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            new StreamerId(314),
            new QueueEntryId(91),
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "complete:314", "fetch"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerIsAlreadyPlaying_When_PromotingWinner_Then_PerformsNoWrite()
    {
        var api = new RecordingStreamerSongListClient(Snapshot(91));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            new StreamerId(314),
            new QueueEntryId(91),
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerMissingFromQueue_When_PromotingWinner_Then_LeavesCurrentSongPlaying()
    {
        var api = new RecordingStreamerSongListClient(new SpinnerQueueSnapshot
        {
            Playing = new SpinnerQueueItem { QueueId = 77 }
        });
        var service = new NowPlayingTransitionService(api);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PromoteWinnerAsync(
                new StreamerSongListChannel("wowfood"),
                new StreamerId(314),
                new QueueEntryId(91),
                TestContext.Current.CancellationToken));

        Assert.Equal(WinnerMissingMessage, error.Message);
        Assert.Equal(["fetch"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerRemovedAfterCompletingCurrent_When_PromotingWinner_Then_ReportsPartialTransition()
    {
        var api = new RecordingStreamerSongListClient(Snapshot(77), new SpinnerQueueSnapshot());
        var service = new NowPlayingTransitionService(api);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PromoteWinnerAsync(
                new StreamerSongListChannel("wowfood"),
                new StreamerId(314),
                new QueueEntryId(91),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            "The previous Now Playing song was marked as played, but the winner could not be promoted. " +
            "Refresh the queue before trying again. " + WinnerMissingMessage,
            error.Message);
        Assert.Equal(WinnerMissingMessage, error.InnerException?.Message);
        Assert.Equal(["fetch", "complete:314", "fetch"], api.Calls);
    }

    private const string WinnerMissingMessage =
        "The selected winner is no longer in the queue. Leave this selection and spin again.";

    private static SpinnerQueueSnapshot Snapshot(int playingId)
    {
        return new SpinnerQueueSnapshot
        {
            Playing = new SpinnerQueueItem { QueueId = playingId },
            Items = [new() { QueueId = 91 }]
        };
    }

    private sealed class RecordingStreamerSongListClient(params SpinnerQueueSnapshot[] snapshots)
        : IStreamerSongListClient
    {
        private readonly Queue<SpinnerQueueSnapshot> _snapshots = new(snapshots);
        public List<string> Calls { get; } = [];

        public Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("fetch");
            return Task.FromResult(_snapshots.Dequeue());
        }

        public Task MarkNowPlayingAsPlayedAsync(
            StreamerId streamerId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"complete:{streamerId}");
            return Task.CompletedTask;
        }

        public Task PromoteQueueItemToNowPlayingAsync(
            QueueEntryId queueEntryId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"promote:{queueEntryId}");
            return Task.CompletedTask;
        }

        public Task<StreamerSongListStreamer> ResolveStreamerAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
            StreamerSongListChannel channel,
            string period = "week",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkQueueItemAsPlayedAsync(
            QueueEntryId queueEntryId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
