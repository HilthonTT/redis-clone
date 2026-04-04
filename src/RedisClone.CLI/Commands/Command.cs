using RedisClone.CLI.Protocol;

namespace RedisClone.CLI.Commands;

// Parses the Redis Serialization Protocol (RESP) array format:
// *<count>\r\n$<len>\r\n<command>\r\n[$<len>\r\n<arg>\r\n...]
public sealed record Command(CommandType Type, string[] Arguments)
{
    public static readonly Command Unknown = new(CommandType.Unknown, []);

    private const int CommandNameIndex = 2;
    private const int FirstArgumentIndex = 4;
    private const int RespStride = 2; // every other token is a length prefix ($N)
    private const int MinTokenCount = 3; // *N, $N, <command

    public static Command Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Unknown;
        }

        string[] tokens = raw
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < MinTokenCount)
        {
            return Unknown;
        }

        if (!Enum.TryParse(tokens[CommandNameIndex], ignoreCase: true, out CommandType commandType))
        {
            return Unknown;
        }

        var arguments = new List<string>();
        for (int i = FirstArgumentIndex; i < tokens.Length; i += RespStride)
        {
            arguments.Add(tokens[i]);
        }

        return new Command(commandType, arguments.ToArray());
    }

    internal static Command FromResp(RespResult result)
    {
        if (result.Type != RespResultType.Array || result.Elements is null || result.Elements.Length == 0)
        {
            return Unknown;
        }

        (string? name, string[]? args) = result.ToCommand();

        if (!Enum.TryParse(name, ignoreCase: true, out CommandType commandType))
        {
            return Unknown;
        }

        return new Command(commandType, args);
    }
}
