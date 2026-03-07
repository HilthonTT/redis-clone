using RedisClone.CLI.Models;
using System.Collections.Frozen;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands;

internal sealed class CommandProcessor(IEnumerable<ICommandHandler> handlers)
{
    private readonly FrozenDictionary<CommandType, ICommandHandler> _handlers =
        handlers.ToFrozenDictionary(h => h.CommandType);

    public RedisValue Process(string rawPayload, Socket socket)
    {
        Command command = Command.Parse(rawPayload);

        if (!_handlers.TryGetValue(command.Type, out var handler))
        {
            Console.WriteLine($"Unknown command: {rawPayload}");
            return RedisValue.ToError("Unknown command");
        }

        RedisValue response = handler.Handle(command, socket);

        return response;
    }
}
