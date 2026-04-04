namespace RedisClone.CLI.Protocol;

internal enum RespResultType
{
    SimpleString,
    Error,
    Integer,
    BulkString,
    Array,
    Null,
}
