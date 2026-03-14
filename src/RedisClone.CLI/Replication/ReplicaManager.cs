using RedisClone.CLI.Commands;
using RedisClone.CLI.Options;
using System.Text;

namespace RedisClone.CLI.Replication;

internal sealed class ReplicaManager(AppSettings settings, CommandProcessor processor) : IAsyncDisposable
{
    private ReplicaClient? _replicationClient;
    private long _offset;
    private CancellationTokenSource? _cts;

    public async Task ConnectToMasterAsync()
    {
        if (settings.Replication.SlaveReplicaSettings is null)
        {
            return;
        }

        try
        {
            var rep = settings.Replication.SlaveReplicaSettings;
            Console.WriteLine($"Connecting to master {rep.MasterHost}:{rep.MasterPort}");

            _cts = new CancellationTokenSource();
            _replicationClient = new ReplicaClient(settings);

            // Handshake
            await _replicationClient.PingAsync();
            await _replicationClient.ConfListeningPortAsync(settings.Runtime.Port);
            await _replicationClient.ConfCapabilitiesAsync();
            await _replicationClient.PSyncAsync("?", -1);

            // Start receive + process loops, re-surface faults via continuation.
            var receiveTask = _replicationClient.WaitForCommandsAsync(_cts.Token);
            var processTask = ProcessCommandsAsync(_cts.Token);

            _ = Task.WhenAny(receiveTask, processTask).ContinueWith(t =>
            {
                if (t.Result.Exception is { } ex)
                {
                    Console.WriteLine($"Replication loop faulted: {ex.GetBaseException().Message}");
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to connect to master: {e}");
        }
    }

    private async Task ProcessCommandsAsync(CancellationToken ct)
    {
        if (_replicationClient is null)
        {
            return;
        }

        await foreach (byte[] payload in _replicationClient.CommandReader.ReadAllAsync(ct))
        {
            try
            {
                await HandleCommandAsync(payload);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to process command from master: {e.Message}");
            }
        }
    }

    private async Task HandleCommandAsync(byte[] payload)
    {
        try
        {
            string rawPayload = Encoding.UTF8.GetString(payload);
            var command = Command.Parse(rawPayload);

            if (command.Type == CommandType.ReplConf &&
                command.Arguments.Length > 0 &&
                command.Arguments[0].Equals("GETACK", StringComparison.OrdinalIgnoreCase) &&
                _replicationClient is not null)
            {
                // Send current offset before counting this command's bytes.
                await _replicationClient.SendAckAsync(Interlocked.Read(ref _offset));
            }
            else
            {
                await processor.Process(rawPayload, _replicationClient!.ClientConnection);
            }
        }
        finally
        {
            // Always advance offset — it tracks bytes received, not bytes processed.
            Interlocked.Add(ref _offset, payload.Length);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_replicationClient is not null)
        {
            await _replicationClient.DisposeAsync();
        }
    }
}
