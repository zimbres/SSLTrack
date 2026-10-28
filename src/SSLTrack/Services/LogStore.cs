namespace SSLTrack.Services;

public sealed class LogStore
{
    public ConcurrentDictionary<int, Log> Logs { get; } = new();
    public ConcurrentDictionary<int, DateTime> Heartbeats { get; } = new();

    private int counter;
    public int Add(Log log)
    {
        var id = Interlocked.Increment(ref counter);
        Logs[id] = log;
        return id;
    }
}
