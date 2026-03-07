using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Get(KvpStorage kvpStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Get;

    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        if (command.Arguments.Length < 1)
        {
            return RedisValue.ToError("ERR wrong number of arguments for 'get'");
        }

        string? value = kvpStorage.Get(command.Arguments[0]);
        return RedisValue.ToBulkString(value);
    }
}
