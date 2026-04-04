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

    public bool Exists(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return false;
        }
        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return false;
        }
        return true;
    }

    public long GetTimeToLive(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return -2; // <- key not found is -2
        }

        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return -2; // <- expired = effectively doesn't exist
        }

        return entry.GetRemainingTtlMs();
    }

    public bool Delete(string key)
    {
        return _store.TryRemove(key, out _);
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


    /// <summary>
    /// Sets or replaces the expiry on an existing key.
    /// Returns false if the key doesn't exist.
    /// </summary>
    public bool SetExpiry(string key, long expireAfterMs)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return false;
        }

        entry.SetExpiry(expireAfterMs);
        return true;
    }

    /// <summary>
    /// Atomically increments the integer value at key by <paramref name="delta"/>.
    /// If the key doesn't exist, it's initialized to 0 before incrementing.
    /// Returns the new value or null if the existing value isn't a valid integer.
    /// </summary>
    public long? IncrementBy(string key, long delta)
    {
        long? result = null;

        _store.AddOrUpdate(
           key,
           _ =>
           {
               result = delta;
               return StorageEntry.Permanent(delta.ToString());
           },
           (_, existing) =>
           {
               if (existing.IsExpired)
               {
                   result = delta;
                   return StorageEntry.Permanent(delta.ToString());
               }

               if (!long.TryParse(existing.Value, out long current))
               {
                   result = null;
                   return existing;
               }

               long newValue = current + delta;
               result = newValue;
               return StorageEntry.Permanent(newValue.ToString());
           });

        return result;
    }

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
