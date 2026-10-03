namespace SonglistSpinner.Simulator;

/// <summary>Failures and holds queued for the next REST requests to a given method and path.</summary>
internal sealed class InjectedFaults
{
    private readonly object _gate = new();
    private readonly List<Failure> _failures = [];
    private readonly List<RequestHold> _holds = [];

    public void Fail(string method, string path, int statusCode, string detail, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (_gate)
            _failures.Add(new Failure(method, path, statusCode, detail) { Remaining = count });
    }

    public RequestHold Hold(string method, string path)
    {
        var hold = new RequestHold(method, path);
        lock (_gate)
            _holds.Add(hold);
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
    public (int StatusCode, string Detail)? TakeFailure(string method, string path)
    {
        lock (_gate)
        {
            var failure = _failures.FirstOrDefault(candidate => Matches(candidate.Method, candidate.Path, method, path));
            if (failure is null) return null;

            failure.Remaining--;
            if (failure.Remaining == 0) _failures.Remove(failure);
            return (failure.StatusCode, failure.Detail);
        }
    }

    private static bool Matches(string expectedMethod, string expectedPath, string method, string path) =>
        string.Equals(expectedMethod, method, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expectedPath, path, StringComparison.OrdinalIgnoreCase);

    private sealed class Failure(string method, string path, int statusCode, string detail)
    {
        public string Method { get; } = method;
        public string Path { get; } = path;
        public int StatusCode { get; } = statusCode;
        public string Detail { get; } = detail;
        public int Remaining { get; set; }
    }
}
