using RedisClone.CLI.Models;
using System.Collections.Frozen;

namespace RedisClone.CLI.Commands;

internal sealed class CommandProcessor(IEnumerable<ICommandHandler> handlers)
{
    private readonly FrozenDictionary<CommandType, ICommandHandler> _handlers =
        handlers.ToFrozenDictionary(h => h.CommandType);

    private static readonly HashSet<CommandType> TransactionControlCommands =
        [CommandType.Exec, CommandType.Discard, CommandType.Multi];

    /// <summary>
    /// Processes a pre-parsed command. Handles transaction queueing for MULTI/EXEC.
    /// </summary>
    public async Task<RedisValue> ProcessCommand(Command command, ClientConnection connection)
    {
        // When in a transaction, queue commands instead of executing —
        // except for EXEC, DISCARD, and nested MULTI.
        if (connection.InTransactionMode && !TransactionControlCommands.Contains(command.Type))
        {
            if (!_handlers.ContainsKey(command.Type))
            {
                return RedisValue.ToError($"ERR unknown command '{command.Type}'");
            }

            connection.QueueCommand(command);
            return RedisValue.QueuedResponse;
        }

        if (!_handlers.TryGetValue(command.Type, out ICommandHandler? handler))
        {
            Console.WriteLine($"Unknown command: {command.Type}");
            return RedisValue.ToError("Unknown command");
        }

        RedisValue response = handler.LongOperation
            ? await handler.HandleAsync(command, connection)
            : handler.Handle(command, connection);

        return response;
    }

    /// <summary>
    /// Executes all queued transaction commands atomically.
    /// Returns a RESP array of results, one per command.
    /// </summary>
    internal async Task<RedisValue> ExecuteTransaction(ClientConnection connection)
    {
        var commands = connection.FlushTransaction();

        if (commands.Count == 0)
        {
            return RedisValue.EmptyBulkStringArray;
        }

        var results = new List<RedisValue>(commands.Count);

        foreach (var command in commands)
        {
            if (!_handlers.TryGetValue(command.Type, out ICommandHandler? handler))
            {
                results.Add(RedisValue.ToError($"ERR unknown command '{command.Type}'"));
                continue;
            }

            try
            {
                RedisValue result = handler.LongOperation
                    ? await handler.HandleAsync(command, connection)
                    : handler.Handle(command, connection);

                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(RedisValue.ToError($"ERR {ex.Message}"));
            }
        }

        return RedisValue.FromArray(results);
    }

    /// <summary>
    /// Processes a raw RESP string. Used by the replication path (ReplicaManager).
    /// </summary>
    public Task<RedisValue> Process(string rawPayload, ClientConnection connection)
    {
        Command command = Command.Parse(rawPayload);
        return ProcessCommand(command, connection);
    }
}
