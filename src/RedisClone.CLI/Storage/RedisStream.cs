namespace RedisClone.CLI.Storage;

/// <summary>
/// Represents a single Redis Stream — an append-only log of entries, each identified
/// by a monotonically increasing ID in <c>timestamp-sequence</c> format.
/// Thread-safe via a dedicated lock.
/// </summary>
internal sealed class RedisStream
{
    private const string AutoId = "*";

    internal sealed record Entry(int SequenceNumber, Dictionary<string, string> Values);

    /// <summary>
    /// A complete stream entry with its full ID and field/value pairs.
    /// </summary>
    internal sealed record StreamEntry(string Id, Dictionary<string, string> Values);

    private readonly SortedSet<long> _timestamps = [];
    private readonly Dictionary<long, LinkedList<Entry>> _entries = [];
    private readonly Lock _lock = new();

    public bool TryAppend(string inputId, Dictionary<string, string> values, out string? id, out string? error)
    {
        id = null;

        lock (_lock)
        {
            if (!TryParseId(inputId, out long timestamp, out int sequenceNumber, out error))
            {
                return false;
            }

            if (!ValidateOrder(timestamp, sequenceNumber, out error))
            {
                return false;
            }

            AppendEntry(timestamp, sequenceNumber, values);
            id = $"{timestamp}-{sequenceNumber}";
            return true;
        }
    }

    /// <summary>
    /// Returns entries with IDs between <paramref name="startId"/> and <paramref name="endId"/> (inclusive).
    /// Supports special values: "-" for minimum, "+" for maximum.
    /// </summary>
    public List<StreamEntry> Range(string startId, string endId)
    {
        lock (_lock)
        {
            ParseRangeId(startId, isStart: true, out long startTs, out int startSeq);
            ParseRangeId(endId, isStart: false, out long endTs, out int endSeq);

            var result = new List<StreamEntry>();

            foreach (long ts in _timestamps.GetViewBetween(startTs, endTs))
            {
                if (!_entries.TryGetValue(ts, out var entries))
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    // Filter by sequence within the boundary timestamps
                    if (ts == startTs && entry.SequenceNumber < startSeq)
                    {
                        continue;
                    }
                    if (ts == endTs && entry.SequenceNumber > endSeq)
                    {
                        continue;
                    }

                    result.Add(new StreamEntry(
                        $"{ts}-{entry.SequenceNumber}",
                        new Dictionary<string, string>(entry.Values)));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Returns entries with IDs strictly greater than <paramref name="afterId"/>.
    /// Used by XREAD.
    /// </summary>
    public List<StreamEntry> ReadAfter(string afterId, int? count = null)
    {
        lock (_lock)
        {
            ParseRangeId(afterId, isStart: true, out long afterTs, out int afterSeq);

            var result = new List<StreamEntry>();

            foreach (long ts in _timestamps)
            {
                if (ts < afterTs)
                {
                    continue;
                }

                if (!_entries.TryGetValue(ts, out var entries))
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    // Strictly greater than the after ID
                    if (ts == afterTs && entry.SequenceNumber <= afterSeq)
                    {
                        continue;
                    }

                    result.Add(new StreamEntry(
                        $"{ts}-{entry.SequenceNumber}",
                        new Dictionary<string, string>(entry.Values)));

                    if (count.HasValue && result.Count >= count.Value)
                    {
                        return result;
                    }
                }
            }

            return result;
        }
    }

    private static void ParseRangeId(string id, bool isStart, out long timestamp, out int sequence)
    {
        // Special range tokens
        if (id == "-")
        {
            timestamp = 0;
            sequence = 0;
            return;
        }

        if (id == "+")
        {
            timestamp = long.MaxValue;
            sequence = int.MaxValue;
            return;
        }

        var parts = id.Split('-');
        if (!long.TryParse(parts[0], out timestamp))
        {
            timestamp = isStart ? 0 : long.MaxValue;
            sequence = isStart ? 0 : int.MaxValue;
            return;
        }

        if (parts.Length < 2 || !int.TryParse(parts[1], out sequence))
        {
            sequence = isStart ? 0 : int.MaxValue;
        }
    }

    private bool TryParseId(string entryKey, out long timestamp, out int sequenceNumber, out string? error)
    {
        sequenceNumber = 0;
        error = null;

        var parts = entryKey.Split('-');

        if (parts[0] == AutoId)
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            sequenceNumber = timestamp == (_timestamps.Count > 0 ? _timestamps.Max : -1)
                ? GetNextSequenceNumber(timestamp) : 0;
            return true;
        }

        if (!long.TryParse(parts[0], out timestamp))
        {
            error = "ERR Invalid stream ID specified as stream command argument";
            return false;
        }

        if (parts.Length < 2 || parts[1] == AutoId)
        {
            sequenceNumber = GetNextSequenceNumber(timestamp);
            return true;
        }

        if (!int.TryParse(parts[1], out sequenceNumber))
        {
            error = "ERR Invalid stream ID specified as stream command argument";
            return false;
        }

        return true;
    }

    private bool ValidateOrder(long timestamp, int sequenceNumber, out string? error)
    {
        error = null;

        if (timestamp < 0 || (timestamp == 0 && sequenceNumber < 1))
        {
            error = "ERR The ID specified in XADD must be greater than 0-0";
            return false;
        }

        if (_timestamps.Count == 0)
        {
            return true;
        }

        long lastTimestamp = _timestamps.Max;
        int lastSequence = _entries[lastTimestamp].Last!.Value.SequenceNumber;

        if (timestamp < lastTimestamp || (timestamp == lastTimestamp && sequenceNumber <= lastSequence))
        {
            error = "ERR The ID specified in XADD is equal or smaller than the target stream top item";
            return false;
        }

        return true;
    }

    private void AppendEntry(long timestamp, int sequenceNumber, Dictionary<string, string> values)
    {
        if (!_entries.TryGetValue(timestamp, out LinkedList<Entry>? value))
        {
            value = new LinkedList<Entry>();
            _entries[timestamp] = value;
            _timestamps.Add(timestamp);
        }

        value.AddLast(new Entry(sequenceNumber, values));
    }

    private int GetNextSequenceNumber(long timestamp)
    {
        if (!_entries.TryGetValue(timestamp, out LinkedList<Entry>? existing))
        {
            return 0;
        }

        return existing.Last!.Value.SequenceNumber + 1;
    }
}
