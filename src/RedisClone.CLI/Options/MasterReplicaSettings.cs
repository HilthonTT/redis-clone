namespace RedisClone.CLI.Options;

public sealed class MasterReplicaSettings
{
    public required string MasterReplicaId { get; set; }

    public required int MasterReplicaOffset { get; set; }
}
