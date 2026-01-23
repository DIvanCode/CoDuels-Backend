namespace Duely.Infrastructure.Telemetry;

public sealed class MetricsSnapshot
{
    private readonly object _lock = new();

    public long WaitingUsers { get; private set; }

    private Dictionary<string, long> _duels = new();
    private Dictionary<string, long> _submissions = new();
    private Dictionary<string, long> _runs = new();
    private Dictionary<string, long> _outbox = new();

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
}
