namespace SonglistSpinner.Simulator;

/// <summary>The simulator's channels, looked up the ways the API addresses them.</summary>
internal sealed class ChannelDirectory(SimulatorContext context)
{
    private readonly List<SimulatedChannel> _channels = [];
    private int _lastStreamerId = 1000;

    public SimulatedChannel Add(string name, string platform, int? streamerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        lock (context.Gate)
        {
            var id = streamerId ?? Interlocked.Increment(ref _lastStreamerId);
            if (_channels.Any(channel => channel.StreamerId == id))
                throw new InvalidOperationException($"Streamer {id} already exists.");

            var channel = new SimulatedChannel(id, name, platform.ToLowerInvariant(), context);
            _channels.Add(channel);
            return channel;
        }
    }

    /// <summary>Finds a channel by name, ignoring case, on <paramref name="platform"/>.</summary>
    public SimulatedChannel? Find(string name, string platform)
    {
        lock (context.Gate)
        {
            return _channels.FirstOrDefault(channel =>
                string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(channel.Platform, platform, StringComparison.Ordinal));
        }
    }

    public SimulatedChannel? Find(int streamerId)
    {
        lock (context.Gate)
            return _channels.FirstOrDefault(channel => channel.StreamerId == streamerId);
    }

    public SimulatedChannel? FindByQueueEntry(int queueId)
    {
        lock (context.Gate)
            return _channels.FirstOrDefault(channel => channel.HasEntry(queueId));
    }
}
