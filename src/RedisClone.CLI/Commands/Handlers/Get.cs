using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Get(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Get;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        string? value = kvpStorage.Get(command.Arguments[0]);
        return RedisValue.ToBulkString(value);
    }
}