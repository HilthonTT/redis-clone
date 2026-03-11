using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

internal abstract class LPopBase(ListStorage listStorage, AppSettings settings) : BaseCommandHandler(settings)
{
    protected bool TryPop(string key, int count, out List<string> removedValues)
    {
        removedValues = [];

        while (count > 0 && listStorage.TryRemoveFirst(key, out var value))
        {
            removedValues.Add(value!);
            count--;
        }

        return removedValues.Count > 0;
    }
}
