using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;
using System.Threading.Channels;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 2)]
internal sealed class BLPop(ListStorage listStorage, AppSettings settings, PubSub pubSub) 
    : LPopBase(listStorage, settings)
{
    public override bool SupportsReplication => false;

    public override CommandType CommandType => CommandType.BLPop;

    public override bool LongOperation => true;

    protected override async Task<RedisValue> HandleSpecificAsync(
        Command command, 
        ClientConnection connection)
    {
        string key = command.Arguments[0];
        double timeoutSec = command.Arguments.Length == 2 ? double.Parse(command.Arguments[1]) : 0;

        // Try immediate pop before subscribing — avoids channel allocation on the happy path.
        if (TryPop(key, 1, out var immediate))
        {
            return RedisValue.ToBulkStringArray([key, immediate[0]]);
        }

        var channel = Channel.CreateBounded<PubSubMessage>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        pubSub.Subscribe(EventType.ListPushed, key, connection.Id, channel);

        try
        {
            using var cts = BuildCancellationTokenSource(timeoutSec);

            await foreach (var _ in channel.Reader.ReadAllAsync(cts.Token))
            {
                if (TryPop(key, 1, out var popped))
                {
                    return RedisValue.ToBulkStringArray([key, popped[0]]);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout elapsed - Redis returns null for BLPOP on timeout.
        }
        finally
        {
            pubSub.Unsubscribe(EventType.ListPushed, key, connection.Id);
            channel.Writer.TryComplete();
        }

        return RedisValue.NullBulkStringArray;
    }

    /// <summary>
    /// timeout == 0 means block indefinitely in Redis semantics.
    /// </summary>
    private static CancellationTokenSource BuildCancellationTokenSource(double timeoutSec) =>
        timeoutSec > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec))
            : new CancellationTokenSource();
}
