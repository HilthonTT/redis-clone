using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands;

internal abstract class BaseCommandHandler(AppSettings settings) : ICommandHandler
{
    public abstract CommandType CommandType { get; }

    protected AppSettings Settings { get; } = settings;

    protected virtual RedisValue HandleSpecific(Command command, Socket socket) => 
        throw new NotImplementedException();

    public RedisValue Handle(Command command, Socket socket)
    {
        return HandleSpecific(command, socket);
    }
}
