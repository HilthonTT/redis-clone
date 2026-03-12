using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Subscriptions;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Unsubscribe(PubSub pubSub, AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Unsubscribe;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string channel = command.Arguments[0];

        int subscriptions = pubSub.Unsubscribe(EventType.Subscription, channel, connection.Id);

        return RedisValue.ToBulkStringArray(["unsubscribe", channel, subscriptions.ToString()]);
    }
}
