using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Persistence;

internal sealed class DataModel
{
    public int RdbVersion { get; set; }
    public List<(string Name, string Value)> Metadata { get; } = [];
    public Dictionary<int, Dictionary<string, StorageEntry>> Databases { get; } = [];
}