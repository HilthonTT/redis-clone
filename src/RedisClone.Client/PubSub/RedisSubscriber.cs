using RedisClone.Client.Protocol;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace RedisClone.Client.PubSub;

/// <summary>
/// A pub/sub subscriber backed by a dedicated Redis connection.
/// Messages are delivered via <see cref="IAsyncEnumerable{RedisMessage}"/>.
///
/// Usage:
/// <code>
/// await using var sub = await client.SubscribeAsync("news", "events");
/// await foreach (var msg in sub.Messages())
/// {
///     Console.WriteLine($"{msg.Channel}: {msg.Message}");
/// }
/// </code>
/// </summary>
public sealed class RedisSubscriber : IAsyncDisposable
{
    private readonly RedisConnection _connection;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<RedisMessage> _channel;
    private readonly Task _listenTask;
    private bool _disposed;

    internal RedisSubscriber(RedisConnection connection, string[] channels)
    {
        _connection = connection;
        _channel = Channel.CreateUnbounded<RedisMessage>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

        // Subscribe to all channels on the dedicated connection
        _listenTask = RunAsync(channels, _cts.Token);
    }

    /// <summary>
    /// Yields messages as they arrive. Completes when the subscriber is disposed
    /// or the connection drops.
    /// </summary>
    public async IAsyncEnumerable<RedisMessage> Messages(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (RedisMessage msg in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return msg;
        }
    }

    /// <summary>
    /// Subscribes to additional channels on this subscriber's connection.
    /// </summary>
    public async Task SubscribeAsync(params string[] channels)
    {
        foreach (string channel in channels)
        {
            await _connection.SendAsync(["SUBSCRIBE", channel], _cts.Token);
        }
    }

    /// <summary>
    /// Unsubscribes from channels. The subscriber remains active for other channels.
    /// </summary>
    public async Task UnsubscribeAsync(params string[] channels)
    {
        foreach(string channel in channels)
        {
            await _connection.SendAsync(["UNSUBSCRIBE", channel], _cts.Token);
        }
    }

    private async Task RunAsync(string[] channels, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send SUBSCRIBE for all initial channels
            foreach (string ch in channels)
            {
                await _connection.SendAsync(["SUBSCRIBE", ch], cancellationToken);
            }

            // Read subscription confirmations + messages

            while (!cancellationToken.IsCancellationRequested)
            {
                RespValue value = await _connection.ReadAsync(cancellationToken);
                if (value.Type != RespType.Array || value.Elements is not { Length: 3 })
                {
                    continue;
                }

                string? kind = value.Elements[0].AsString();
                string? channel = value.Elements[1].AsString();
                string? payload = value.Elements[2].AsString();


                if (kind == "message" && channel is not null && payload is not null)
                {
                    _channel.Writer.TryWrite(new RedisMessage(channel, payload));
                }
                // "subscribe" and "unsubscribe" confirmations are silently consumed
            }
        }
        catch (OperationCanceledException) { /* expected on dispose */ }
        catch (IOException) { /* connection dropped */ }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await _cts.CancelAsync();
        _channel.Writer.TryComplete();

        try 
        {
            await _listenTask; 
        }
        catch (OperationCanceledException) { }

        await _connection.DisposeAsync();
        _cts.Dispose();
    }
}