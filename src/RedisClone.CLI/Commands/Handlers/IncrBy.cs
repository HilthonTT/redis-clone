using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2, max: 2)]
internal sealed class IncrBy(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.IncrBy;

    public override bool SupportsReplication => true;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        if (!long.TryParse(command.Arguments[1], out long delta))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        long? result = kvpStorage.IncrementBy(command.Arguments[0], delta);

        return result.HasValue
            ? RedisValue.ToLongValue(result.Value)
            : RedisValue.ToError("ERR value is not an integer or out of range");
    }
}
