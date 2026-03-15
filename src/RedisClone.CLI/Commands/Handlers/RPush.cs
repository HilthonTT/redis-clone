using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2)]
[ReplicationRole(role: ReplicationRole.Master)]
internal sealed class RPush(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override bool SupportsReplication => true;

    public override CommandType CommandType => CommandType.RPush;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        int count = listStorage.AddLast(command.Arguments[0], command.Arguments.Skip(1));

        return RedisValue.ToIntegerValue(count);
    }
}
