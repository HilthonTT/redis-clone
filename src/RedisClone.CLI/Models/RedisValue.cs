using RedisClone.CLI.Commands;
using System.Text;

namespace RedisClone.CLI.Models;

public sealed record RedisValue(RedisType Type, byte[] Value)
{
    public const string OkValue = "+OK\r\n";

    private static readonly byte[] OkPayload =
        Encoding.UTF8.GetBytes(OkValue);

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

    public static RedisValue ToBulkStringArray(Command command)
    {
        List<string> values = [command.Type.ToString()];
        var finalValues = values.Concat(command.Arguments);

        return ToBulkStringArray(finalValues);
    }

    public static RedisValue ToBinaryContent(byte[] bytes)
    {
        byte[] prefix = Encoding.UTF8.GetBytes($"${bytes.Length}\r\n");
        return new RedisValue(RedisType.BinaryContent, prefix.Concat(bytes).ToArray());
    }

    public static RedisValue ToIntegerValue(int value)
    {
        return new RedisValue(RedisType.Integer, Encoding.UTF8.GetBytes(ToIntegerString(value)));
    }

    public static RedisValue FromArray(IEnumerable<RedisValue> values)
    {
        var list = values as IList<RedisValue> ?? values.ToList();

        // Calculate total size upfront to avoid repeated allocations
        int headerSize = Encoding.UTF8.GetByteCount($"*{list.Count}\r\n");
        int totalSize = headerSize + list.Sum(v => v.Value.Length);

        var buffer = new byte[totalSize];
        int offset = 0;

        offset += Encoding.UTF8.GetBytes($"*{list.Count}\r\n", buffer.AsSpan(offset));

        foreach (var value in list)
        {
            value.Value.CopyTo(buffer, offset);
            offset += value.Value.Length;
        }

        return new RedisValue(RedisType.BulkStringArray, buffer);
    }

    public static string ToIntegerString(int value) => $":{value}\r\n";

    private static string ToBulkStringContent(string str) => $"${str.Length}\r\n{str}\r\n";
}

