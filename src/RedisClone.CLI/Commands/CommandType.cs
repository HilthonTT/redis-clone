namespace RedisClone.CLI.Commands;

public enum CommandType
{
    Unknown,

    Get,
    Set,
    Keys,
    Type,

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

    Subscribe,
    Publish,
    Unsubscribe,

    ReplConf,
    PSync,
    Wait,

    XAdd,
}