using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 1)]
internal sealed class Decr(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Decr;

    public override bool SupportsReplication => true;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        long? result = kvpStorage.IncrementBy(command.Arguments[0], -1);

        return result.HasValue
            ? RedisValue.ToLongValue(result.Value)
            : RedisValue.ToError("ERR value is not an integer or out of range");
    }
}
