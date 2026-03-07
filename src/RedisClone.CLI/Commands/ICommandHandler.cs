using RedisClone.CLI.Models;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands;

internal interface ICommandHandler
{
    CommandType CommandType { get; }

    RedisValue Handle(Command command, Socket socket);
}