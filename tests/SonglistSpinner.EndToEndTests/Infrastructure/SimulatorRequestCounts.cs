using SonglistSpinner.Simulator;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// Waits on the simulator's request log for a number of matching calls, for actions that repeat a call: a channel
/// load fetches the queue once, and the realtime connection it opens fetches it again.
/// </summary>
internal static class SimulatorRequestCounts
{
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(EndToEnd.ExpectTimeoutMilliseconds);

    /// <summary>
    /// Completes with the first <paramref name="count"/> matching requests in the log, oldest first, once there are
    /// that many. The log hands back the same record for a request every time, so each wait skips the ones already
    /// counted by reference, and checking the log and waiting happen under the log's lock.
    /// </summary>
    /// <exception cref="TimeoutException">Fewer than <paramref name="count"/> arrived within the expectation timeout.</exception>
    public static async Task<IReadOnlyList<RecordedRequest>> WaitForRequestCountAsync(
        this StreamerSongListSimulator simulator,
        Func<RecordedRequest, bool> match,
        int count,
        CancellationToken cancellationToken)
    {
        var counted = new List<RecordedRequest>();
        while (counted.Count < count)
        {
            try
            {
                counted.Add(await simulator
                    .WaitForFirstRequestAsync(
                        request => match(request) && !counted.Exists(seen => ReferenceEquals(seen, request)),
                        cancellationToken)
                    .WaitAsync(Limit, cancellationToken));
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    $"Expected {count} matching requests within {Limit.TotalSeconds} s, but only {counted.Count} arrived.",
                    ex);
            }
        }

        return counted;
    }
}
