using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Ping(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Ping;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        return RedisValue.ToSimpleString("PONG");
    }
}
