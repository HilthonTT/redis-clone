using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Type(StorageManager storageManager, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Type;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];
        ValueType keyType = storageManager.GetType(key);

        return RedisValue.ToSimpleString(keyType.ToString().ToLowerInvariant());
    }
}
