using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 1)]
internal sealed class LLen(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.LLen;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        if (!listStorage.TryGetList(command.Arguments[0], out IReadOnlyCollection<string>? list))
        {
            return RedisValue.ToIntegerValue(0);
        }

        return RedisValue.ToIntegerValue(list?.Count ?? 0);
    }
}
