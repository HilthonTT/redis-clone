using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2, max: 4)]
internal sealed class Set(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    private const string ExpiryFlag = "PX";

    public override CommandType CommandType => CommandType.Set;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];
        string value = command.Arguments[1];
        kvpStorage.Set(key, value, TryParseExpiry(command.Arguments));
        return RedisValue.Ok;
    }

    private static long? TryParseExpiry(string[] arguments)
    {
        if (arguments.Length == 4
            && arguments[2].Equals(ExpiryFlag, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(arguments[3], out long ms))
        {
            return ms;
        }
        return null;
    }
}
