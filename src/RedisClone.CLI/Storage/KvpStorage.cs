using System.Collections.Concurrent;

namespace RedisClone.CLI.Storage;

internal sealed class KvpStorage : IDisposable
{
    private readonly ConcurrentDictionary<string, StorageEntry> _store = new();
    private readonly Timer _evictionTimer;
    private const int EvictionIntervalMs = 5_000;

    public KvpStorage()
    {
        _evictionTimer = new Timer(
            _ => EvictExpiredKeys(),
            state: null,
            dueTime: EvictionIntervalMs,
            period: EvictionIntervalMs);
    }

    public IEnumerable<string> Keys => _store.Keys;

    public string? Get(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return null;
        }

        return entry.Value;
    }

    public void Set(string key, string value, long? expireAfterMs = null)
    {
        var entry = expireAfterMs.HasValue
            ? StorageEntry.WithExpiry(value, expireAfterMs.Value)
            : StorageEntry.Permanent(value);

        _store[key] = entry;
    }

    public void Initialize(Dictionary<string, StorageEntry> loadedData)
    {
        foreach (var entry in loadedData)
        {
            _store.TryAdd(entry.Key, entry.Value);
        }
    }

    public bool Remove(string key) => _store.TryRemove(key, out _);

    private void EvictExpiredKeys()
    {
        foreach (var (key, entry) in _store)
        {
            if (entry.IsExpired)
            {
                _store.TryRemove(key, out _);
            }
        }
    }

    public void Dispose() => _evictionTimer.Dispose();
}
