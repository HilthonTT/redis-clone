namespace RedisClone.CLI.Options;

public sealed class PersistenceSettings
{
    public required string Directory { get; set; }

    public required string DbFileName { get; set; }
}