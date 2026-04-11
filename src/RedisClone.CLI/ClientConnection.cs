using RedisClone.CLI.Commands;
using RedisClone.CLI.Logging;
using RedisClone.CLI.Models;
using RedisClone.CLI.Subscriptions;
using System.Net.Sockets;
using System.Threading.Channels;

namespace RedisClone.CLI;

internal sealed class ClientConnection(int id, Socket socket) : IAsyncDisposable
{
    // Unbounded allows the PubSub system to enqueue without blocking;
    // swap for BoundedChannelOptions if you want backpressure.
    private readonly Channel<PubSubMessage> _messageChannel =
        Channel.CreateUnbounded<PubSubMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,  // only PubSubBroadcast reads
            SingleWriter = false  // multiple publishers may write
        });

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _modeLock = new(1, 1);
    private readonly List<Command> _transactionQueue = [];

    private Task? _broadcastTask;
    private bool _disposed;

    public int Id { get; } = id;
    public Socket Socket { get; } = socket;
    public bool InSubscribedMode { get; private set; }

    public long LastCommandOffset { get; set; }

    public bool IsAuthenticated { get; private set; }

    public bool IsReplicaConnection => Id == -1;

    // The writer is exposed so PubSub can enqueue without touching internals.
    public ChannelWriter<PubSubMessage> MessageWriter => _messageChannel.Writer;

    public bool InTransactionMode { get; private set; }

    public void EnterTransactionMode()
    {
        InTransactionMode = true;
        _transactionQueue.Clear();
    }

    public void QueueCommand(Command command) => _transactionQueue.Add(command);

    public List<Command> FlushTransaction()
    {
        var commands = new List<Command>(_transactionQueue);
        _transactionQueue.Clear();
        InTransactionMode = false;
        return commands;
    }

    public void DiscardTransaction()
    {
        _transactionQueue.Clear();
        InTransactionMode = false;
    }

    public async Task EnterSubscribedModeAsync()
    {
        await _modeLock.WaitAsync(_cts.Token);
        try
        {
            if (InSubscribedMode)
            {
                return;
            }

            InSubscribedMode = true;
            _broadcastTask = Task.Run(() => PubSubBroadcastAsync(_cts.Token), _cts.Token);

            RespLogger.Escape($"Client {Id}: entered subscribed mode");
        }
        finally
        {
            _modeLock.Release();
        }
    }

    public void Authenticate() => IsAuthenticated = true;

    // Call this once after construction so open-mode servers
    // don't require AUTH at all.
    public void SetInitialAuthState(bool requiresAuth)
    {
        IsAuthenticated = !requiresAuth;
    }

    private async Task PubSubBroadcastAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _messageChannel.Reader.ReadAllAsync(ct))
            {
                RespLogger.Escape(
                    $"Client {Id}: sending channel={message.Channel} message={message.Message}");

                var payload = RedisValue.ToBulkStringArray(["message", message.Channel, message.Message]);

                try
                {
                    await Socket.SendAsync(payload.Value, ct);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine(
                        $"Client {Id}: socket error on send (channel={message.Channel}): {ex.Message}. Closing broadcast loop.");

                    // A broken socket won't recover — stop immediately.
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
        finally
        {
            RespLogger.Escape($"Client {Id}: broadcast loop exited");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal the broadcast loop to stop and drain the channel.
        await _cts.CancelAsync();
        _messageChannel.Writer.TryComplete();

        if (_broadcastTask is not null)
        {
            try
            {
                await _broadcastTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation.
            }
        }

        _cts.Dispose();
        _modeLock.Dispose();
        Socket.Dispose();
    }
}
