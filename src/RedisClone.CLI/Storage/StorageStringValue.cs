namespace RedisClone.CLI.Storage;

internal sealed record StorageEntry
{
    public string Value { get; }
    private readonly DateTimeOffset? _expiresAt;

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
}
