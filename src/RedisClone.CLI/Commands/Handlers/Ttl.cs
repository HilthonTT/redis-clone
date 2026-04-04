using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Ttl(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Ttl;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        long ttlMs = kvpStorage.GetTimeToLive(command.Arguments[0]);
        long ttlSec = ttlMs > 0 ? ttlMs / 1000 : ttlMs;
        return RedisValue.ToLongValue(ttlSec);
    }
}