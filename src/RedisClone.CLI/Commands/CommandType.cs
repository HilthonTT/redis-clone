namespace RedisClone.CLI.Commands;

public enum CommandType
{
    Unknown,

    // Strings
    Get,
    Set,
    Incr,
    Decr,
    IncrBy,
    DecrBy,

    // Keys
    Keys,
    Type,
    Del,
    Exists,
    Expire,
    PExpire,
    Ttl,

    // Server
    Ping,
    Echo,
    Config,
    Info,

    // Lists
    LLen,
    LPush,
    RPush,
    LRange,
    LPop,
    RPop,
    BLPop,

    // Pub/Sub
    Subscribe,
    Publish,
    Unsubscribe,

    // Replication
    ReplConf,
    PSync,
    Wait,

    // Streams
    XAdd,
    XRange,
    XRead,

    // Transactions
    Multi,
    Exec,
    Discard,
}
