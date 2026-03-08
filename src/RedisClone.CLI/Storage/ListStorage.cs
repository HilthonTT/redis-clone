using System.Collections.Concurrent;

namespace RedisClone.CLI.Storage;

internal sealed class ListStorage
{
    private readonly ConcurrentDictionary<string, LinkedList<string>> _store = new();

    public IEnumerable<string> Keys => _store.Keys;

    public bool TryGetList(string key, out IReadOnlyCollection<string>? list)
    {
        list = null;
        if (_store.TryGetValue(key, out var linkedList))
        {
            return false;
        }

        list = linkedList;

        return true;
    }

    public int AddFirst(string key, IEnumerable<string> values) =>
        Push(key, values, (list, value) => list.AddFirst(value));

    public int AddLast(string key, IEnumerable<string> values) =>
        Push(key, values, (list, value) => list.AddLast(value));

    public bool TryRemoveFirst(string key, out string? value)
    {
        value = null;
        if (!_store.TryGetValue(key, out var list))
        {
            return false;
        }

        value = list.First!.Value;
        list.RemoveFirst();

        if (list.Count == 0)
        {
            _store.TryRemove(key, out _);
        }

        return true;
    }

    public bool TryRemoveLast(string key, out string? value)
    {
        value = null;
        if (!_store.TryGetValue(key, out var list))
        {
            return false;
        }

        value = list.Last!.Value;
        list.RemoveLast();

        if (list.Count == 0) _store.TryRemove(key, out _);

        return true;
    }

    private int Push(string key, IEnumerable<string> values, Action<LinkedList<string>, string> addToFirst)
    {
        var list = GetOrAdd(key);

        foreach (var value in values)
        {
            // TODO: Publish pubsub
            addToFirst(list, value);
        }

        return list.Count;
    }

    private LinkedList<string> GetOrAdd(string key) => _store.GetOrAdd(key, _ => new LinkedList<string>());
}
