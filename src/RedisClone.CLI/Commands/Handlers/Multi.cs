using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Multi(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Multi;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        if (connection.InTransactionMode)
        {
            return RedisValue.ToError("ERR MULTI calls can not be nested");
        }

        connection.EnterTransactionMode();
        return RedisValue.Ok;
    }
}