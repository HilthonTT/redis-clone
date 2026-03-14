using RedisClone.CLI.Commands;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RedisClone.CLI.Replication;

internal sealed class MasterManager(AppSettings settings) : IAsyncDisposable
{
    private const string EmptyRdbFile =
        "UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==";

    private readonly ConcurrentDictionary<int, Replica> _replicas = new();
    private readonly ReplicationLog _replicationLog = new();
    private readonly SemaphoreSlim _replicationSignal = new(0);
    private CancellationTokenSource? _cts;

    public void StartReplication()
    {
        if (settings.Replication.Role != ReplicationRole.Master)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _ = RunReplicationLoopAsync(_cts.Token).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Console.WriteLine($"Replication loop terminated unexpectedly: {t.Exception!.GetBaseException().Message}");
            }
        });
    }

    public async Task InitReplicaConnectionAsync(ClientConnection connection, RedisValue pSyncResponse)
    {
        RedisValue rdbBytes = RedisValue.ToBinaryContent(Convert.FromBase64String(EmptyRdbFile));

        var socket = connection.Socket;
        await socket.SendAsync(pSyncResponse.Value, SocketFlags.None);
        await socket.SendAsync(rdbBytes.Value, SocketFlags.None);

        var replica = new Replica(connection.Id, socket);
        _replicas[connection.Id] = replica;

        // Signal the replication loop that a new replica is ready.
        _replicationSignal.Release();

        Console.WriteLine($"Registered replica {replica.Id}");
    }

    public long PropagateCommand(Command command)
    {
        var commandBytes = RedisValue.ToBulkStringArray(command).Value;
        var offset = _replicationLog.Append(commandBytes);

        // Signal the loop instead of waiting for the next poll tick.
        _replicationSignal.Release();

        return offset;
    }

    public int CountReplicasWithAckOffset(long offset) =>
        _replicas.Values.Count(r => r.AckOffset >= offset);

    public async Task RequestAckFromAllReplicasAsync()
    {
        var tasks = _replicas.Values.Select(r => r.GetAckOffsetAsync(_replicationLog.Offset));
        await Task.WhenAll(tasks);
    }

    public void SetReplicaAckOffset(long offset, ClientConnection connection)
    {
        if (_replicas.TryGetValue(connection.Id, out var replica))
        {
            replica.AckOffset = offset;
        }
        else
        {
            Console.WriteLine($"No replica found for connection {connection.Id}");
        }
    }

    private async Task RunReplicationLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait until a command is propagated or a replica connects.
            await _replicationSignal.WaitAsync(ct);

            foreach (var replica in _replicas.Values)
            {
                try
                {
                    await ReplicateToAsync(replica, ct);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to replicate to replica {replica.Id}: {e.Message}");
                    // Remove dead replica to stop sending to it.
                    _replicas.TryRemove(replica.Id, out _);
                }
            }
        }
    }

    /// <summary>
    /// Sends all pending commands to a single replica sequentially,
    /// then advances the replica's send offset.
    /// Sequential send is intentional — RESP command order must be preserved.
    /// </summary>
    private async Task ReplicateToAsync(Replica replica, CancellationToken ct)
    {
        var commands = _replicationLog.GetCommandsToReplicate(replica.Offset);
        if (commands.Count == 0)
        {
            return;
        }

        foreach (ReadOnlyMemory<byte> payload in commands)
        {
            ct.ThrowIfCancellationRequested();
            await replica.SendAsync(payload);
        }

        // Advance send offset to the end of the last command sent.
        var lastCommand = commands[^1];
        // Offset advances by total bytes sent — replica.Offset was the start,
        // so new offset = old offset + sum of all payload lengths.
        replica.Offset += commands.Sum(c => c.Length);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        _replicationSignal.Dispose();
    }
}
