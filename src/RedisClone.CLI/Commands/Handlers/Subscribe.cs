using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Subscriptions;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Subscribe(AppSettings settings, PubSub pubSub) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Subscribe;

    public override bool LongOperation => true;

    protected override async Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection)
    {
        await connection.EnterSubscribedModeAsync();

        var responses = new List<RedisValue>(command.Arguments.Length);

        foreach (string channel in command.Arguments)
        {
            int count = pubSub.Subscribe(
                EventType.Subscription,
                channel,
                connection.Id,
                connection.MessageWriter); 

            responses.Add(RedisValue.ToBulkStringArray(["subscribe", channel, count.ToString()]));
        }

        return RedisValue.FromArray(responses);
    }
}
