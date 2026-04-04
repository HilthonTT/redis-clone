using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 2)]
internal sealed class RPop(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override bool SupportsReplication => false;

    public override CommandType CommandType => CommandType.RPop;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];
        bool hasCountArg = command.Arguments.Length == 2;

        if (hasCountArg && !int.TryParse(command.Arguments[1], out int _))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        int popCount = hasCountArg ? int.Parse(command.Arguments[1]) : 1;
        var removedValues = new List<string>();

        for (int i = 0; i < popCount; i++)
        {
            if (!listStorage.TryRemoveLast(key, out var value))
            {
                break;
            }
            removedValues.Add(value!);
        }

        if (removedValues.Count == 0)
        {
            return RedisValue.EmptyBulkStringArray;
        }

        return hasCountArg
            ? RedisValue.ToBulkStringArray(removedValues)
            : RedisValue.ToBulkString(removedValues[0]);
    }
}
