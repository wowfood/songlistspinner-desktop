using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace SonglistSpinner.Core.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that reports each timer as it is created. Advancing the clock before
/// a background task has started its delay would skip that delay's timer, so a test waits for the timer
/// first and then advances by exactly its due time.
/// </summary>
internal sealed class TimerTrackingTimeProvider : FakeTimeProvider
{
    private readonly Channel<TimeSpan> _createdTimers = Channel.CreateUnbounded<TimeSpan>();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        _createdTimers.Writer.TryWrite(dueTime);
        return timer;
    }

    /// <summary>Waits for the next timer to be created and returns its due time.</summary>
    public ValueTask<TimeSpan> WaitForTimerAsync(CancellationToken cancellationToken) =>
        _createdTimers.Reader.ReadAsync(cancellationToken);
}
