using System.Collections.Concurrent;

namespace RedisClone.CLI.Storage;

internal sealed class StreamStorage
{
    private readonly ConcurrentDictionary<string, RedisStream> _store = new();

    public IEnumerable<string> Keys => _store.Keys;

    public bool TryAppend(string streamKey, string inputId, Dictionary<string, string> values, out string? id, out string? error)
    {
        var stream = _store.GetOrAdd(streamKey, _ => new RedisStream());
        return stream.TryAppend(inputId, values, out id, out error);
    }

    /// <summary>
    /// XRANGE: returns entries between start and end IDs (inclusive).
    /// </summary>
    public List<RedisStream.StreamEntry> Range(string streamKey, string start, string end)
    {
        if (!_store.TryGetValue(streamKey, out var stream))
        {
            return [];
        }

        return stream.Range(start, end);
    }

    /// <summary>
    /// XREAD: returns entries with IDs strictly greater than afterId.
    /// </summary>
    public List<RedisStream.StreamEntry> ReadAfter(string streamKey, string afterId, int? count = null)
    {
        if (!_store.TryGetValue(streamKey, out var stream))
        {
            return [];
        }

        return stream.ReadAfter(afterId, count);
    }

    public bool HasKey(string streamKey) => _store.ContainsKey(streamKey);

    public bool Remove(string key) => _store.TryRemove(key, out _);
}