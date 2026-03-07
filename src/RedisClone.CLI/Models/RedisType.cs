namespace RedisClone.CLI.Models;

public enum RedisType
{
    Unknown,
    SimpleString,
    BulkString,
    BulkStringArray,
    ErrorString,
    BinaryContent,
    ReplicaConnection,
    Integer,
    Void
}