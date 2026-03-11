using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 1)]
internal sealed class LLen(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.LLen;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        if (!listStorage.TryGetList(command.Arguments[0], out IReadOnlyCollection<string>? list))
        {
            return RedisValue.ToIntegerValue(0);
        }

        return RedisValue.ToIntegerValue(list?.Count ?? 0);
    }
}
