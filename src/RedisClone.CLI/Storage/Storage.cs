namespace RedisClone.CLI.Storage;

internal sealed class Storage(KvpStorage kvpStorage)
{
    public KvpStorage KvpStorage { get; } = kvpStorage;
}
