using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2, max: 2)]
internal sealed class PExpire(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.PExpire;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];

        if (!long.TryParse(command.Arguments[1], out long ms))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        bool set = kvpStorage.SetExpiry(key, ms);
        return RedisValue.ToIntegerValue(set ? 1 : 0);
    }
}