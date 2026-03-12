namespace RedisClone.CLI.Storage;

internal sealed class StorageManager(
    KvpStorage kvpStorage,
    ListStorage listStorage, 
    StreamStorage streamStorage)
{
    public KvpStorage KvpStorage { get; } = kvpStorage;

    public ListStorage ListStorage { get; } = listStorage;

    public StreamStorage StreamStorage { get; } = streamStorage;

    public IEnumerable<string> GetAllKeys()
    {
        return KvpStorage.Keys.Union(ListStorage.Keys).Order();
    }

    public ValueType GetType(string key)
    {
        if (KvpStorage.Get(key) is not null)
        {
            return ValueType.String;
        }

        if (ListStorage.TryGetList(key, out _))
        {
            return ValueType.List;
        }

        if (StreamStorage.HasKey(key))
        {
            return ValueType.Stream;
        }

        return ValueType.None;
    }
}
