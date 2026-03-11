namespace RedisClone.CLI.Commands;

public enum CommandType
{
    Unknown,

    Get,
    Set,

    Ping,
    Echo,

    Config,
    Info,

    LLen,
    LPush,
    RPush,
    LRange,
    LPop,
    BLPop,
}