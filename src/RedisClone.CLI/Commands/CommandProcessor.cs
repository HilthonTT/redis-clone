using RedisClone.CLI.Models;
using System.Collections.Frozen;

namespace RedisClone.CLI.Commands;

internal sealed class CommandProcessor(IEnumerable<ICommandHandler> handlers)
{
    private readonly FrozenDictionary<CommandType, ICommandHandler> _handlers =
        handlers.ToFrozenDictionary(h => h.CommandType);

    public async Task<RedisValue> Process(string rawPayload, ClientConnection connection)
    {
        Command command = Command.Parse(rawPayload);

        if (!_handlers.TryGetValue(command.Type, out ICommandHandler? handler))
        {
            Console.WriteLine($"Unknown command: {rawPayload}");
            return RedisValue.ToError("Unknown command");
        }

        RedisValue response = handler.LongOperation 
            ? await handler.HandleAsync(command, connection) 
            : handler.Handle(command, connection);

        return response;
    }
}
