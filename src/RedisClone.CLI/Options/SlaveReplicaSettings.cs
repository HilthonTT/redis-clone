namespace RedisClone.CLI.Options;

public sealed class SlaveReplicaSettings
{
    public required string MasterHost { get; set; }

    public required int MasterPort { get; set; }
}
