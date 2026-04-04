using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2)]
internal sealed class LPush(
    AppSettings settings,
    ListStorage listStorage,
    PubSub pubSub) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.LPush;

    public override bool SupportsReplication => true;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string key = command.Arguments[0];
        int count = listStorage.AddFirst(key, command.Arguments.Skip(1));

        // Notify any BLPOP waiters for each value pushed.
        for (int i = 1; i < command.Arguments.Length; i++)
        {
            pubSub.Publish(EventType.ListPushed, key, command.Arguments[i]);
        }

        return RedisValue.ToIntegerValue(count);
    }
}
