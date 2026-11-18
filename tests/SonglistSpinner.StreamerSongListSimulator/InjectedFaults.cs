namespace SonglistSpinner.Simulator;

/// <summary>Failures and holds queued for the next REST requests to a given method and path.</summary>
internal sealed class InjectedFaults
{
    private readonly object _gate = new();
    private readonly List<Failure> _failures = [];
    private readonly List<RequestHold> _holds = [];

    // Every hold handed out since the last Clear, taken or not, so Clear can release a request still held.
    private readonly List<RequestHold> _issuedHolds = [];

    public void Fail(string method, string path, int statusCode, string detail, int count) =>
        Add(new Failure(method, path, new InjectedFailure(statusCode, detail, AbortConnection: false)), count);

    public void Abort(string method, string path, int count) =>
        Add(new Failure(method, path, new InjectedFailure(0, "", AbortConnection: true)), count);

    public RequestHold Hold(string method, string path)
    {
        var hold = new RequestHold(method, path);
        lock (_gate)
        {
            _holds.Add(hold);
            _issuedHolds.Add(hold);
        }

        return hold;
    }

    public RequestHold? TakeHold(string method, string path)
    {
        lock (_gate)
        {
            var hold = _holds.FirstOrDefault(candidate => Matches(candidate.Method, candidate.Path, method, path));
            if (hold is not null) _holds.Remove(hold);
            return hold;
        }
    }

    /// <summary>Consumes one use of the first failure queued for this request, if any.</summary>
    public InjectedFailure? TakeFailure(string method, string path)
    {
        lock (_gate)
        {
            var failure = _failures.FirstOrDefault(candidate => Matches(candidate.Method, candidate.Path, method, path));
            if (failure is null) return null;

            failure.Remaining--;
            if (failure.Remaining == 0) _failures.Remove(failure);
            return failure.Outcome;
        }
    }

    /// <summary>
    /// Forgets every queued failure and hold, and releases every hold handed out, so a request that is held now
    /// is answered and none is held later.
    /// </summary>
    public void Clear()
    {
        RequestHold[] issued;
        lock (_gate)
        {
            _failures.Clear();
            _holds.Clear();
            issued = [.. _issuedHolds];
            _issuedHolds.Clear();
        }

        foreach (var hold in issued)
            hold.Release();
    }

    private void Add(Failure failure, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        failure.Remaining = count;
        lock (_gate)
            _failures.Add(failure);
    }

    private static bool Matches(string expectedMethod, string expectedPath, string method, string path) =>
        string.Equals(expectedMethod, method, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expectedPath, path, StringComparison.OrdinalIgnoreCase);

    private sealed class Failure(string method, string path, InjectedFailure outcome)
    {
        public string Method { get; } = method;
        public string Path { get; } = path;
        public InjectedFailure Outcome { get; } = outcome;
        public int Remaining { get; set; }
    }
}

/// <summary>
/// How an injected failure answers: a problem response with <see cref="StatusCode"/> and <see cref="Detail"/>, or,
/// with <see cref="AbortConnection"/>, no response at all.
/// </summary>
internal sealed record InjectedFailure(int StatusCode, string Detail, bool AbortConnection);
