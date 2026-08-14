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
        var api = new RecordingSpinnerApiService(new SpinnerQueueSnapshot());
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            314,
            91,
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "promote:91"], api.Calls);
    }

    [Fact]
    public async Task Given_CurrentSong_When_PromotingWinner_Then_CompletesCurrentBeforePromotion()
    {
        var api = new RecordingSpinnerApiService(
            Snapshot(77),
            Snapshot(42));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            314,
            91,
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "complete:314", "fetch", "promote:91"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerWasAutoPromoted_When_CompletingCurrent_Then_DoesNotPromoteTwice()
    {
        var api = new RecordingSpinnerApiService(
            Snapshot(77),
            Snapshot(91));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            314,
            91,
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "complete:314", "fetch"], api.Calls);
    }

    [Fact]
    public async Task Given_WinnerIsAlreadyPlaying_When_PromotingWinner_Then_PerformsNoWrite()
    {
        var api = new RecordingSpinnerApiService(Snapshot(91));
        var service = new NowPlayingTransitionService(api);

        await service.PromoteWinnerAsync(
            new StreamerSongListChannel("wowfood"),
            314,
            91,
            TestContext.Current.CancellationToken);

        Assert.Equal(["fetch"], api.Calls);
    }

    private static SpinnerQueueSnapshot Snapshot(int playingId)
    {
        return new SpinnerQueueSnapshot
        {
            Playing = new SpinnerQueueItem { QueueId = playingId }
        };
    }

    private sealed class RecordingSpinnerApiService(params SpinnerQueueSnapshot[] snapshots)
        : ISpinnerApiService
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
            int streamerId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"complete:{streamerId}");
            return Task.CompletedTask;
        }

        public Task PromoteQueueItemToNowPlayingAsync(
            int queueId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"promote:{queueId}");
            return Task.CompletedTask;
        }

        public Task<int> ResolveStreamerIdAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SpinnerQueueItem[]> FetchQueueAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
            StreamerSongListChannel channel,
            string period = "week",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkQueueItemAsPlayedAsync(
            int queueId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
