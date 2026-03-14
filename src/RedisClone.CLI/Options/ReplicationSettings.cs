namespace RedisClone.CLI.Options;

public sealed class ReplicationSettings
{
    public required ReplicationRole Role { get; set; }

    public MasterReplicaSettings? MasterReplicaSettings { get; set; }

    public SlaveReplicaSettings? SlaveReplicaSettings { get; set; }
}
