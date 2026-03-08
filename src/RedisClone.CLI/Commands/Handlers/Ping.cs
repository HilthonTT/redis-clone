using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Ping(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Ping;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        return RedisValue.ToSimpleString("PONG");
    }
}
