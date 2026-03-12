using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 2)]
internal sealed class LLPop(AppSettings settings, ListStorage listStorage) : LPopBase(listStorage, settings)
{
    public override CommandType CommandType => CommandType.LPop;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];
        bool hasCountArg = command.Arguments.Length == 2;

        if (hasCountArg && !int.TryParse(command.Arguments[1], out int _))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        int popCount = hasCountArg ? int.Parse(command.Arguments[1]) : 1;

        if (!TryPop(key, popCount, out List<string> removedValues))
        {
            return RedisValue.EmptyBulkStringArray;
        }

        // When no count arg is given, Redis returns a single bulk string, not an array
        return hasCountArg
            ? RedisValue.ToBulkStringArray(removedValues)
            : RedisValue.ToBulkString(removedValues[0]);
    }
}
