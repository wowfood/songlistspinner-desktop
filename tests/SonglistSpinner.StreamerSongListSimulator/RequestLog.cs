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
    public Task<RecordedRequest> WaitForNextAsync(Func<RecordedRequest, bool> match, CancellationToken cancellationToken) =>
        WaitAsync(match, includeAnswered: false, cancellationToken);

    /// <summary>
    /// Completes with the first matching request in the log, oldest first, or with the next one answered when the
    /// log has none. Checking the log and registering the waiter happen under one lock, so no request is missed.
    /// </summary>
    public Task<RecordedRequest> WaitForFirstAsync(Func<RecordedRequest, bool> match, CancellationToken cancellationToken) =>
        WaitAsync(match, includeAnswered: true, cancellationToken);

    /// <summary>Empties the log. Waiters still pending are cancelled: they belong to whoever cleared it.</summary>
    public void Clear()
    {
        List<TaskCompletionSource<RecordedRequest>> pending;
        lock (_gate)
        {
            _requests.Clear();
            pending = _waiters.Select(waiter => waiter.Waiter).ToList();
            _waiters.Clear();
        }

        foreach (var waiter in pending)
            waiter.TrySetCanceled();
    }

    private Task<RecordedRequest> WaitAsync(
        Func<RecordedRequest, bool> match,
        bool includeAnswered,
        CancellationToken cancellationToken)
    {
        var waiter = new TaskCompletionSource<RecordedRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            var answered = includeAnswered ? _requests.FirstOrDefault(match) : null;
            if (answered is not null) return Task.FromResult(answered);
            _waiters.Add((match, waiter));
        }

        return waiter.Task.WaitAsync(cancellationToken);
    }
}
