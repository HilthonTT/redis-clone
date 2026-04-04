using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Delete(StorageManager storage, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Del;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        int removed = 0;
        foreach (string key in command.Arguments)
        {
            if (storage.Delete(key))
            {
                removed++;
            }
        }

        return RedisValue.ToIntegerValue(removed);
    }
}