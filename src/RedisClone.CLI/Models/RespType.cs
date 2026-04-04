namespace RedisClone.CLI.Models;

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