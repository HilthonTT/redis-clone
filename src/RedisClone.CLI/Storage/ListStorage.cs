using System.Collections.Concurrent;

namespace RedisClone.CLI.Storage;

internal sealed class ListStorage
{
    private readonly ConcurrentDictionary<string, LinkedList<string>> _store = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public IEnumerable<string> Keys => _store.Keys;

    public bool TryGetList(string key, out IReadOnlyCollection<string>? list)
    {
        list = null;
        if (!_store.TryGetValue(key, out var linkedList))
        {
            return false;
        }

        lock (GetLock(key))
        {
            // Re-check under lock — another thread may have removed it.
            if (!_store.TryGetValue(key, out linkedList))
            {
                return false;
            }

            // Return a snapshot to avoid external iteration while mutating.
            list = linkedList.ToList();
            return true;
        }
    }

    public int AddFirst(string key, IEnumerable<string> values) =>
        Push(key, values, (list, value) => list.AddFirst(value));

    public int AddLast(string key, IEnumerable<string> values) =>
        Push(key, values, (list, value) => list.AddLast(value));

    public bool TryRemoveFirst(string key, out string? value)
    {
        value = null;

        lock (GetLock(key))
        {
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
    }

    public bool TryRemoveLast(string key, out string? value)
    {
        value = null;

        lock (GetLock(key))
        {
            if (!_store.TryGetValue(key, out var list) || list.Count == 0)
            {
                return false;
            }

            value = list.Last!.Value;
            list.RemoveLast();

            if (list.Count == 0) _store.TryRemove(key, out _);

            return true;
        }
    }

    private int Push(string key, IEnumerable<string> values, Action<LinkedList<string>, string> addAction)
    {
        lock (GetLock(key))
        {
            var list = _store.GetOrAdd(key, _ => new LinkedList<string>());

            foreach (var value in values)
            {
                addAction(list, value);
            }

            return list.Count;
        }
    }

    public bool Remove(string key)
    {
        lock (GetLock(key))
        {
            bool removed = _store.TryRemove(key, out _);
            if (removed)
            {
                _locks.TryRemove(key, out _);
            }
            return removed;
        }
    }

    private object GetLock(string key) => _locks.GetOrAdd(key, _ => new object());
}
