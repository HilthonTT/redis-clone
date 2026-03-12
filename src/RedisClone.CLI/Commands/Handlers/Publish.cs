using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Subscriptions;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2)]
internal sealed class Publish(AppSettings settings, PubSub pubSub) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Publish;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string channel = command.Arguments[0];
        string message = command.Arguments[1];

        int deliveries = pubSub.Publish(EventType.Subscription, channel, message);

        return RedisValue.ToIntegerValue(deliveries);
    }
}
