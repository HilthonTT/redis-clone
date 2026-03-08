using System.Collections.Concurrent;

namespace RedisClone.CLI.Storage;

internal sealed class StreamStorage
{
    private readonly ConcurrentDictionary<string, RedisStream> _store = new();

    public bool TryAppend(string streamKey, string inputId, Dictionary<string, string> values, out string? id, out string? error)
    {
        var stream = _store.GetOrAdd(streamKey, _ => new RedisStream());
        return stream.TryAppend(inputId, values, out id, out error);
    }

    public bool HasKey(string streamKey) => _store.ContainsKey(streamKey);
}
