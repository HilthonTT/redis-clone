using System.Text;

namespace RedisClone.CLI.Models;

public sealed record RedisValue(RedisType Type, byte[] Value)
{
    private static readonly byte[] OkPayload =
        Encoding.UTF8.GetBytes("+OK\r\n");

    private static readonly byte[] NilPayload =
        Encoding.UTF8.GetBytes("$-1\r\n");

    public static readonly RedisValue Ok =
        new(RedisType.SimpleString, OkPayload);

    public static readonly RedisValue UnknownCommandError =
        ToError("ERR unknown command");

    public bool Success => Type != RedisType.ErrorString;

    public static RedisValue ToError(string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"-{message}\r\n");
        return new RedisValue(RedisType.ErrorString, payload);
    }

    public static RedisValue ToBulkString(string? value)
    {
        if (value is null)
        {
            return new RedisValue(RedisType.BulkString, NilPayload);
        }

        byte[] encoded = Encoding.UTF8.GetBytes(value);
        byte[] payload = Encoding.UTF8.GetBytes($"${encoded.Length}\r\n{value}\r\n");
        return new RedisValue(RedisType.BulkString, payload);
    }
}