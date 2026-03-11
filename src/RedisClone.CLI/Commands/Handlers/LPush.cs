using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2)]
internal sealed class LPush(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.LPush;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        int count = listStorage.AddFirst(command.Arguments[0], command.Arguments.Skip(1));
        return RedisValue.ToIntegerValue(count);
    }
}
