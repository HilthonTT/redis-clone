using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Discard(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Discard;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        if (!connection.InTransactionMode)
        {
            return RedisValue.ToError("ERR DISCARD without MULTI");
        }

        connection.DiscardTransaction();
        return RedisValue.Ok;
    }
}
