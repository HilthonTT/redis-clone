using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Set(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    private const string ExpiryFlag = "PX";
    private const int ArgumentsWithExpiry = 4;

    public override CommandType CommandType => CommandType.Set;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        if (command.Arguments.Length < 2)
        {
            return RedisValue.ToError("ERR wrong number of arguments for 'set'");
        }

        string key = command.Arguments[0];
        string value = command.Arguments[1];
        long? expiresAfterMs = TryParseExpiry(command.Arguments);

        kvpStorage.Set(key, value, expiresAfterMs);
        return RedisValue.Ok;
    }

    private static long? TryParseExpiry(string[] arguments)
    {
        if (arguments.Length == ArgumentsWithExpiry
            && arguments[2].Equals(ExpiryFlag, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(arguments[3], out long ms))
        {
            return ms;
        }
        return null;
    }
}
