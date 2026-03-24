namespace RedisClone.Client.Protocol;

public enum RespType
{
    SimpleString,
    Error,
    Integer,
    BulkString,
    Array,
    Null,
    NullArray,
}
