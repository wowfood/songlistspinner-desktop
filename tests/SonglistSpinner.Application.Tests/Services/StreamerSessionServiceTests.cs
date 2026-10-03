using System.Threading.Channels;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class StreamerSessionServiceTests
{
    private const string Streamer = "wowfood";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Given_RealtimeRefreshFails_When_NoFurtherEventArrives_Then_RetriesUntilRefreshSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedSpinnerApi();
        api.QueueResponses.Enqueue(() => throw new IOException("Simulated transient API failure"));
        api.QueueResponses.Enqueue(() => Task.FromResult(QueueWith(41)));
        var events = new ChannelEventSource();
        await using var session = new StreamerSessionService(api, events, new OverlayStateService());
        var recovered = WaitForRecoveryAfterFailure(session);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [], [], null, cancellationToken);

        events.Publish(StreamerSongListEventKind.QueueChanged);
        var snapshot = await recovered.WaitAsync(WaitLimit, cancellationToken);

        Assert.Equal(2, api.QueueFetches);
        Assert.Equal(StreamerSessionHealth.Healthy, snapshot.ApiHealth);
        Assert.Equal([41], snapshot.AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RefreshSuspended_When_Refreshing_Then_SkipsTheApiAndKeepsTheSnapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedSpinnerApi();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), new OverlayStateService());
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        session.SetRefreshSuspended(true);

        var refreshed = await session.RefreshAsync(Streamer, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal(0, api.QueueFetches);
        Assert.Equal([7], session.GetSnapshot().AvailableSongs.Select(song => song.QueueId));
    }

    [Fact]
    public async Task Given_RefreshInFlight_When_SpinSuspendsRefresh_Then_FetchedQueueIsNotPublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var api = new ScriptedSpinnerApi();
        var pendingQueue = new TaskCompletionSource<SpinnerQueueSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        api.QueueResponses.Enqueue(() => pendingQueue.Task);
        var overlay = new OverlayStateService();
        await using var session = new StreamerSessionService(api, new ChannelEventSource(), overlay);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        var refresh = session.RefreshAsync(Streamer, cancellationToken);
        await api.QueueFetchStarted.Task.WaitAsync(WaitLimit, cancellationToken);

        session.SetRefreshSuspended(true);
        pendingQueue.SetResult(QueueWith(41, 42));
        var refreshed = await refresh.WaitAsync(WaitLimit, cancellationToken);

        Assert.Null(refreshed);
        Assert.Equal([7], session.GetSnapshot().AvailableSongs.Select(song => song.QueueId));
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal(1, overlayState.RootElement.GetProperty("availableCount").GetInt32());
    }

    [Fact]
    public async Task Given_RefreshSuspended_When_PublishingAnEmptyQueue_Then_OverlayShowsNoSongs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var overlay = new OverlayStateService();
        await using var session = new StreamerSessionService(
            new ScriptedSpinnerApi(),
            new ChannelEventSource(),
            overlay);
        await session.StartAsync(1, Streamer, new SpinnerConfig(), [Song(7)], [], null, cancellationToken);
        session.SetRefreshSuspended(true);

        await session.UpdateSnapshotAsync(new SpinnerConfig(), [], [], null, cancellationToken);

        Assert.Empty(session.GetSnapshot().AvailableSongs);
        using var overlayState = await OverlayStateServiceTests.ReadInitialStateAsync(overlay);
        Assert.Equal(0, overlayState.RootElement.GetProperty("availableCount").GetInt32());
    }

    private static Task<StreamerSessionSnapshot> WaitForRecoveryAfterFailure(StreamerSessionService session)
    {
        var recovered = new TaskCompletionSource<StreamerSessionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = false;
        session.Changed += (_, e) =>
        {
            if (e.Snapshot.ApiHealth == StreamerSessionHealth.Failed)
                failed = true;
            else if (failed && e.Snapshot.ApiHealth == StreamerSessionHealth.Healthy)
                recovered.TrySetResult(e.Snapshot);
        };
        return recovered.Task;
    }

    private static SpinnerQueueItem Song(int queueId) => new() { QueueId = queueId };

    private static SpinnerQueueSnapshot QueueWith(params int[] queueIds) =>
        new() { Items = [.. queueIds.Select(Song)] };

    private sealed class ScriptedSpinnerApi : ISpinnerApiService
    {
        private int _queueFetches;

        /// <summary>Responses for successive queue fetches; an empty queue is returned once these run out.</summary>
        public Queue<Func<Task<SpinnerQueueSnapshot>>> QueueResponses { get; } = new();

        public TaskCompletionSource QueueFetchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int QueueFetches => Volatile.Read(ref _queueFetches);

        public Task<SpinnerQueueSnapshot> FetchQueueSnapshotAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _queueFetches);
            QueueFetchStarted.TrySetResult();
            Func<Task<SpinnerQueueSnapshot>> respond;
            lock (QueueResponses)
            {
                if (!QueueResponses.TryDequeue(out respond!))
                    return Task.FromResult(new SpinnerQueueSnapshot());
            }

            return respond();
        }

        public Task<PlayHistoryItem[]> FetchPlayHistoryAsync(
            StreamerSongListChannel channel,
            string period = "week",
            CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<PlayHistoryItem>());

        public Task<StreamerSongListStreamer> ResolveStreamerAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> ResolveStreamerIdAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SpinnerQueueItem[]> FetchQueueAsync(
            StreamerSongListChannel channel,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkQueueItemAsPlayedAsync(
            int queueId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkNowPlayingAsPlayedAsync(
            int streamerId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task PromoteQueueItemToNowPlayingAsync(
            int queueId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ChannelEventSource : IStreamerSongListEventSource
    {
        private readonly Channel<StreamerSongListEvent> _events = Channel.CreateUnbounded<StreamerSongListEvent>();

        public void Publish(StreamerSongListEventKind kind) =>
            _events.Writer.TryWrite(new StreamerSongListEvent(kind));

        public IAsyncEnumerable<StreamerSongListEvent> SubscribeAsync(
            int streamerId,
            CancellationToken cancellationToken = default) => _events.Reader.ReadAllAsync(cancellationToken);
    }
}
