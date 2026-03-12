using RedisClone.CLI.Models;

namespace RedisClone.CLI.Commands;

internal interface ICommandHandler
{
    CommandType CommandType { get; }

    bool LongOperation { get; }

    RedisValue Handle(Command command, ClientConnection connection);

    Task<RedisValue> HandleAsync(Command command, ClientConnection connection);
}