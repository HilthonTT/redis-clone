using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Echo(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Echo;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        string value = command.Arguments[0];
        return RedisValue.ToSimpleString(value);
    }
}
