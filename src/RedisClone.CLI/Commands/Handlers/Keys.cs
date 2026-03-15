using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Keys(StorageManager storageManager, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Keys;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string pattern = command.Arguments[0].ToUpperInvariant();

        if (pattern != "*")
        {
            return RedisValue.ToError($"Unsupported keys pattern: {pattern}");
        }

        return RedisValue.ToBulkStringArray(storageManager.GetAllKeys());
    }
}
