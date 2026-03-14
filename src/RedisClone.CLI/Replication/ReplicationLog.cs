namespace RedisClone.CLI.Replication;

public sealed class ReplicationLog
{
    private readonly record struct Entry(long Offset, ReadOnlyMemory<byte> Payload);

    private readonly List<Entry> _entries = [];
    private readonly Lock _lock = new();
    private long _nextOffset;

    /// <summary>The current replication offset (total bytes appended).</summary>
    public long Offset
    {
        get { lock (_lock) return _nextOffset; }
    }

    public long Append(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new ArgumentException("Payload must not be empty.", nameof(payload));
        }

        lock (_lock)
        {
            var offset = _nextOffset;

            _entries.Add(new Entry(offset, payload.ToArray()));
            _nextOffset = offset + payload.Length;
            return _nextOffset;
        }
    }

    public IReadOnlyList<ReadOnlyMemory<byte>> GetCommandsToReplicate(long startOffset)
    {
        if (startOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Offset must be non-negative.");
        }

        lock (_lock)
        {
            if (startOffset >= _nextOffset || _entries.Count == 0)
            {
                return [];
            }

            int index = FindEntryIndex(startOffset);
            var result = new List<ReadOnlyMemory<byte>>(_entries.Count - index);

            for (int i = index; i < _entries.Count; i++)
            {
                result.Add(_entries[i].Payload);
            }

            return result;
        }
    }

    public void TrimBefore(long upToOffset)
    {
        lock (_lock)
        {
            int removeCount = 0;
            foreach (var entry in _entries)
            {
                if (entry.Offset + entry.Payload.Length <= upToOffset)
                {
                    removeCount++;
                }
                else
                {
                    break;
                }
            }

            if (removeCount > 0)
            {
                _entries.RemoveRange(0, removeCount);
            }
        }
    }

    private int FindEntryIndex(long startOffset)
    {
        int lo = 0;
        int hi = _entries.Count - 1;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_entries[mid].Offset < startOffset)
            {
                lo = mid + 1;
            }
            else
            {
                lo = mid;
            }
        }

        // If the found entry starts after startOffset, the replica's offset falls
        // inside the previous entry — step back one to include it.
        if (lo > 0 && _entries[lo].Offset > startOffset)
        {
            lo--;
        }

        return lo;
    }
}
