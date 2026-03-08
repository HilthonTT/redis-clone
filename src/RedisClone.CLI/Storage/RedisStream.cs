namespace RedisClone.CLI.Storage;

/// <summary>
/// Represents a single Redis Stream — an append-only log of entries, each identified
/// by a monotonically increasing ID in <c>timestamp-sequence</c> format.
/// </summary>
/// <remarks>
/// Mirrors the Redis Stream data structure underpinning commands such as
/// <c>XADD</c>, <c>XREAD</c>, and <c>XRANGE</c>.
/// IDs must always increase — entries cannot be inserted out of order.
/// </remarks>
internal sealed class RedisStream
{
    /// <summary>
    /// The sentinel value used by clients to request a fully auto-generated entry ID.
    /// </summary>
    /// <example>
    /// Input: <c>"*"</c> → Output: <c>"1704067200000-0"</c> (timestamp + sequence auto-assigned)
    /// </example>
    private const string AutoId = "*";

    /// <summary>
    /// A single entry within the stream, holding its sequence number and
    /// a set of field/value pairs.
    /// </summary>
    /// <example>
    /// SequenceNumber: <c>2</c>, Values: <c>{ "name": "hans", "age": "25" }</c>
    /// </example>
    private sealed record Entry(int SequenceNumber, Dictionary<string, string> Values);

    /// <summary>
    /// Sorted set of all timestamps present in the stream.
    /// Enables O(log n) lookup of the latest timestamp via <see cref="SortedSet{T}.Max"/>.
    /// </summary>
    private readonly SortedSet<long> _timestamps = [];

    /// <summary>
    /// Maps each timestamp to its ordered list of entries.
    /// Multiple entries can share a timestamp, differentiated by their sequence number.
    /// </summary>
    /// <example>
    /// Key: <c>1704067200000</c> → Value: <c>[Entry(0, ...), Entry(1, ...), Entry(2, ...)]</c>
    /// </example>
    private readonly Dictionary<long, LinkedList<Entry>> _entries = [];

    /// <summary>
    /// Attempts to append a new entry to the stream, corresponding to the Redis <c>XADD</c> command.
    /// </summary>
    /// <param name="inputId">
    /// The requested entry ID. Accepts the following formats:
    /// <list type="bullet">
    ///   <item><c>"*"</c> — auto-generate both timestamp and sequence</item>
    ///   <item><c>"1704067200000-*"</c> — use provided timestamp, auto-generate sequence</item>
    ///   <item><c>"1704067200000-3"</c> — use exactly the provided ID</item>
    /// </list>
    /// </param>
    /// <param name="values">The field/value pairs for this entry, e.g. <c>{ "name": "hans" }</c>.</param>
    /// <param name="id">
    /// When successful, the final assigned ID, e.g. <c>"1704067200000-0"</c>. 
    /// <c>null</c> on failure.
    /// </param>
    /// <param name="error">
    /// When unsuccessful, a RESP-formatted error string. <c>null</c> on success.
    /// </param>
    /// <returns><c>true</c> if the entry was appended; <c>false</c> otherwise.</returns>
    /// <example>
    /// Input:  inputId=<c>"*"</c>, values=<c>{ "name": "hans" }</c>
    /// Output: id=<c>"1704067200000-0"</c>, returns <c>true</c>
    ///
    /// Input:  inputId=<c>"0-0"</c>, values=<c>{ "name": "hans" }</c>
    /// Output: error=<c>"ERR The ID specified in XADD must be greater than 0-0"</c>, returns <c>false</c>
    /// </example>
    public bool TryAppend(string inputId, Dictionary<string, string> values, out string? id, out string? error)
    {
        id = null;

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

    /// <summary>
    /// Parses a raw input ID string into its <paramref name="timestamp"/> and
    /// <paramref name="sequenceNumber"/> components, auto-generating either or both if requested.
    /// </summary>
    /// <param name="entryKey">The raw ID string from the client.</param>
    /// <param name="timestamp">The parsed or generated Unix millisecond timestamp.</param>
    /// <param name="sequenceNumber">The parsed or generated sequence number.</param>
    /// <param name="error">Populated with a RESP error string if parsing fails.</param>
    /// <returns><c>true</c> if parsing succeeded; <c>false</c> otherwise.</returns>
    /// <example>
    /// Input: <c>"1704067200000-3"</c> → timestamp=<c>1704067200000</c>, sequence=<c>3</c>
    /// Input: <c>"1704067200000-*"</c> → timestamp=<c>1704067200000</c>, sequence=<c>next available</c>
    /// Input: <c>"*"</c>              → timestamp=<c>UtcNow ms</c>,      sequence=<c>0 or next</c>
    /// Input: <c>"abc-1"</c>          → error=<c>"ERR Invalid stream ID..."</c>, returns <c>false</c>
    /// </example>
    private bool TryParseId(string entryKey, out long timestamp, out int sequenceNumber, out string? error)
    {
        sequenceNumber = 0;
        error = null;

        var parts = entryKey.Split('-');

        // Both parts are auto-generated
        if (parts[0] == AutoId)
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            sequenceNumber = timestamp == (_timestamps.Count > 0 ? _timestamps.Max : -1) ?
                GetNextSequenceNumber(timestamp) : 0;
            return true;
        }

        if (!long.TryParse(parts[0], out timestamp))
        {
            error = "ERR Invalid stream ID specified as stream command argument";
            return false;
        }

        // Sequence is auto-generated
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

    /// <summary>
    /// Validates that the given ID is strictly greater than the current stream tail,
    /// enforcing Redis's monotonically increasing ID requirement.
    /// </summary>
    /// <param name="timestamp">The timestamp component of the candidate ID.</param>
    /// <param name="sequenceNumber">The sequence component of the candidate ID.</param>
    /// <param name="error">Populated with a RESP error string if validation fails.</param>
    /// <returns><c>true</c> if the ID is valid and in order; <c>false</c> otherwise.</returns>
    /// <example>
    /// Stream tail: <c>"1704067200000-2"</c>
    ///
    /// Input: timestamp=<c>1704067200000</c>, sequence=<c>3</c> → returns <c>true</c>  (greater sequence)
    /// Input: timestamp=<c>1704067200001</c>, sequence=<c>0</c> → returns <c>true</c>  (greater timestamp)
    /// Input: timestamp=<c>1704067200000</c>, sequence=<c>2</c> → returns <c>false</c> (equal — not allowed)
    /// Input: timestamp=<c>1704067199999</c>, sequence=<c>9</c> → returns <c>false</c> (older timestamp)
    /// Input: timestamp=<c>0</c>,             sequence=<c>0</c> → returns <c>false</c> (below minimum 0-1)
    /// </example>
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

    /// <summary>
    /// Persists a new entry under the given timestamp, initialising the timestamp
    /// bucket if this is the first entry for that timestamp.
    /// </summary>
    /// <param name="timestamp">The Unix millisecond timestamp for the entry.</param>
    /// <param name="sequenceNumber">The sequence number within that timestamp.</param>
    /// <param name="values">The field/value pairs to store.</param>
    /// <example>
    /// AppendEntry(<c>1704067200000</c>, <c>0</c>, <c>{ "name": "hans" }</c>)
    /// → _entries[1704067200000] = [Entry(0, { "name": "hans" })]
    ///
    /// AppendEntry(<c>1704067200000</c>, <c>1</c>, <c>{ "name": "lena" }</c>)
    /// → _entries[1704067200000] = [Entry(0, { "name": "hans" }), Entry(1, { "name": "lena" })]
    /// </example>
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

    /// <summary>
    /// Returns the next available sequence number for a given timestamp.
    /// Returns <c>0</c> if no entries exist yet for that timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp to look up.</param>
    /// <returns>
    /// The last sequence number for this timestamp incremented by one,
    /// or <c>0</c> if this timestamp has no entries yet.
    /// </returns>
    /// <example>
    /// _entries[1704067200000] = [Entry(0, ...), Entry(1, ...), Entry(2, ...)]
    ///
    /// Input: timestamp=<c>1704067200000</c> → returns <c>3</c>
    /// Input: timestamp=<c>9999999999999</c> → returns <c>0</c> (no entries for this timestamp)
    /// </example>
    private int GetNextSequenceNumber(long timestamp)
    {
        if (!_entries.TryGetValue(timestamp, out LinkedList<Entry>? existing))
        {
            return 0;
        }

        return existing.Last!.Value.SequenceNumber + 1;
    }
}
