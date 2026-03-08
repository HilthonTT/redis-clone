namespace RedisClone.CLI.Commands;

public enum CommandType
{
    Unknown,

    Get,
    Set,

    Ping,
    Echo,
}