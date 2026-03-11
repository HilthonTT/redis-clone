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

    public static readonly RedisValue NullBulkStringArray =
        new(RedisType.BulkString, Encoding.UTF8.GetBytes("$-1\r\n"));

    public static readonly RedisValue EmptyBulkStringArray =
        new(RedisType.BulkString, Encoding.UTF8.GetBytes("$*0\r\n"));

    public bool Success => Type != RedisType.ErrorString;

    public static RedisValue ToError(string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"-{message}\r\n");
        return new RedisValue(RedisType.ErrorString, payload);
    }

    public static RedisValue ToSimpleString(string value)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"+{value}\r\n");
        return new RedisValue(RedisType.SimpleString, payload);
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

    public static RedisValue ToBulkStringArray(IEnumerable<string> values)
    {
        var list = values as IList<string> ?? values.ToList();
        var sb = new StringBuilder();

        sb.Append($"*{list.Count}\r\n");
        foreach (string value in list)
        {
            sb.Append(ToBulkStringContent(value));
        }

        return new RedisValue(RedisType.BulkString, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public static RedisValue ToIntegerValue(int value)
    {
        return new RedisValue(RedisType.Integer, Encoding.UTF8.GetBytes(ToIntegerString(value));
    }

    public static string ToIntegerString(int value) => $":{value}\r\n";

    private static string ToBulkStringContent(string str) => $"${str.Length}\r\n{str}\r\n";
}