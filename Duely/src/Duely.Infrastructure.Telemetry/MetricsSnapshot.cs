namespace Duely.Infrastructure.Telemetry;

public sealed class MetricsSnapshot
{
    private readonly object _lock = new();

    public long WaitingUsers { get; private set; }

    private Dictionary<string, long> _duels = new();
    private Dictionary<string, long> _submissions = new();
    private Dictionary<string, long> _runs = new();
    private Dictionary<string, long> _outbox = new();
    private Dictionary<string, long> _queuedStale = new();
    private Dictionary<string, double> _queuedOldestAge = new();
    private Dictionary<(string Type, string Status), long> _outboxDispatch = new();
    private Dictionary<string, long> _pollerLastSuccess = new();

    public long TestingFailedLastFiveMinutes { get; private set; }

    public void SetWaitingUsers(long value)
    {
        lock (_lock) WaitingUsers = value;
    }

    public void SetDuels(Dictionary<string, long> values)
    {
        lock (_lock) _duels = values;
    }

    public void SetSubmissions(Dictionary<string, long> values)
    {
        lock (_lock) _submissions = values;
    }

    public void SetCodeRuns(Dictionary<string, long> values)
    {
        lock (_lock) _runs = values;
    }

    public void SetOutboxMessages(Dictionary<string, long> values)
    {
        lock (_lock) _outbox = values;
    }

    public void SetQueued(string kind, long stale, double oldestAgeSeconds)
    {
        lock (_lock)
        {
            _queuedStale[kind] = stale;
            _queuedOldestAge[kind] = oldestAgeSeconds;
        }
    }

    public void SetTestingFailedLastFiveMinutes(long value)
    {
        lock (_lock) TestingFailedLastFiveMinutes = value;
    }

    public void SetOutboxDispatch(Dictionary<(string Type, string Status), long> values)
    {
        lock (_lock) _outboxDispatch = values;
    }

    public void RegisterPoller(string source)
    {
        lock (_lock) _pollerLastSuccess.TryAdd(source, 0);
    }

    public void SetPollerLastSuccess(string source, long unixSeconds)
    {
        lock (_lock) _pollerLastSuccess[source] = unixSeconds;
    }

    public IReadOnlyDictionary<string, long> GetDuels()
    {
        lock (_lock) return new Dictionary<string, long>(_duels);
    }

    public IReadOnlyDictionary<string, long> GetSubmissions()
    {
        lock (_lock) return new Dictionary<string, long>(_submissions);
    }

    public IReadOnlyDictionary<string, long> GetRuns()
    {
        lock (_lock) return new Dictionary<string, long>(_runs);
    }

    public IReadOnlyDictionary<string, long> GetOutbox()
    {
        lock (_lock) return new Dictionary<string, long>(_outbox);
    }

    public IReadOnlyDictionary<string, long> GetQueuedStale()
    {
        lock (_lock) return new Dictionary<string, long>(_queuedStale);
    }

    public IReadOnlyDictionary<string, double> GetQueuedOldestAge()
    {
        lock (_lock) return new Dictionary<string, double>(_queuedOldestAge);
    }

    public IReadOnlyDictionary<(string Type, string Status), long> GetOutboxDispatch()
    {
        lock (_lock) return new Dictionary<(string Type, string Status), long>(_outboxDispatch);
    }

    public IReadOnlyDictionary<string, long> GetPollerLastSuccess()
    {
        lock (_lock) return new Dictionary<string, long>(_pollerLastSuccess);
    }
}
