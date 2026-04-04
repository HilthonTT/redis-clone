namespace RedisClone.CLI.Storage;

internal sealed record StorageEntry
{
    public string Value { get; }

    private DateTimeOffset? _expiresAt;

    private StorageEntry(string value, DateTimeOffset? expiresAt)
    {
        Value = value;
        _expiresAt = expiresAt;
    }

    public bool IsExpired =>
        _expiresAt.HasValue && _expiresAt.Value < DateTimeOffset.UtcNow;

    public static StorageEntry Permanent(string value) =>
        new(value, expiresAt: null);

    public static StorageEntry WithExpiry(string value, long expireAfterMs) =>
        new(value, DateTimeOffset.UtcNow.AddMilliseconds(expireAfterMs));

    /// <summary>
    /// Sets or replaces the expiry on this entry.
    /// </summary>
    public void SetExpiry(long expireAfterMs)
    {
        _expiresAt = DateTimeOffset.UtcNow.AddMilliseconds(expireAfterMs);
    }

    /// <summary>
    /// Removes the expiry, making this entry permanent.
    /// </summary>
    public void RemoveExpiry()
    {
        _expiresAt = null;
    }

    public long GetRemainingTtlMs()
    {
        if (!_expiresAt.HasValue)
        {
            return -1; // No expiry
        }

        long ttl = (long)(_expiresAt.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
        return ttl > 0 ? ttl : -2; // -2 if expired
    }
}
