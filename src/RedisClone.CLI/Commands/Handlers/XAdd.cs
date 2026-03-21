using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 4)]
[ReplicationRole(role: ReplicationRole.Master)]
internal sealed class XAdd(AppSettings settings, StreamStorage storage) : BaseCommandHandler(settings)
{
    public override bool SupportsReplication => true;

    public override CommandType CommandType => CommandType.XAdd;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        // Arguments: <streamKey> <id> <field> <value> [<field> <value> ...]
        // Fields start at index 2, so their count must be even.
        int fieldCount = command.Arguments.Length - 2;
        if (fieldCount % 2 != 0)
        {
            return RedisValue.ToError("ERR wrong number of arguments for 'xadd' command");
        }

        string streamKey = command.Arguments[0];
        string inputId = command.Arguments[1];

        Dictionary<string, string> entries = command.Arguments
            .Skip(2)
            .Chunk(2)
            .ToDictionary(pair => pair[0], pair => pair[1]);

        if(!storage.TryAppend(streamKey, inputId, entries, out var id, out var error))
        {
            return RedisValue.ToError(error ?? "ERR failed to append to stream");
        }

        return RedisValue.ToBulkString(id);
    }
}
