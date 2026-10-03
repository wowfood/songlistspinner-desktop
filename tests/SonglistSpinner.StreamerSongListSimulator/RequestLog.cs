namespace SonglistSpinner.Simulator;

/// <summary>The REST requests answered so far, and waiters for the next one that matches.</summary>
internal sealed class RequestLog
{
    private readonly object _gate = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly List<(Func<RecordedRequest, bool> Match, TaskCompletionSource<RecordedRequest> Waiter)> _waiters = [];

    public IReadOnlyList<RecordedRequest> Snapshot()
    {
        lock (_gate)
            return _requests.ToArray();
    }

    public void Record(RecordedRequest request)
    {
        List<TaskCompletionSource<RecordedRequest>> matched = [];
        lock (_gate)
        {
            _requests.Add(request);
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (!_waiters[i].Match(request)) continue;
                matched.Add(_waiters[i].Waiter);
                _waiters.RemoveAt(i);
            }
        }

        foreach (var waiter in matched)
            waiter.TrySetResult(request);
    }

    /// <summary>Completes with the first matching request answered after this call; earlier ones do not count.</summary>
    public Task<RecordedRequest> WaitForNextAsync(Func<RecordedRequest, bool> match, CancellationToken cancellationToken)
    {
        var waiter = new TaskCompletionSource<RecordedRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
            _waiters.Add((match, waiter));
        return waiter.Task.WaitAsync(cancellationToken);
    }
}
