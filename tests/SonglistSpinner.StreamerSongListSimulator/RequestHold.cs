namespace SonglistSpinner.Simulator;

/// <summary>
/// Holds the next matching REST request before it is answered, until <see cref="Release"/> is called or the
/// client gives up. Use it to make a response slow without waiting on the clock.
/// </summary>
public sealed class RequestHold
{
    private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RequestHold(string method, string path)
    {
        Method = method;
        Path = path;
    }

    internal string Method { get; }

    internal string Path { get; }

    /// <summary>Completes when the held request reaches the simulator.</summary>
    public Task Arrived => _arrived.Task;

    /// <summary>Lets the held request, and any that arrive later, be answered.</summary>
    public void Release() => _released.TrySetResult();

    internal async Task WaitAsync(CancellationToken requestAborted)
    {
        _arrived.TrySetResult();
        await _released.Task.WaitAsync(requestAborted);
    }
}
