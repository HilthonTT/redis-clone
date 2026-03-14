using RedisClone.CLI.Models;
using System.Net.Sockets;

namespace RedisClone.CLI.Replication;

public sealed class Replica(int id, Socket socket)
{
    public int Id { get; } = id;
    public Socket Socket { get; } = socket;

    /// <summary>
    /// Bytes of replication log confirmed sent to this replica.
    /// Owned and advanced exclusively by <see cref="MasterManager"/>.
    /// </summary>
    public long Offset { get; internal set; }

    /// <summary>
    /// Bytes the replica has confirmed processing via REPLCONF ACK.
    /// Written by <see cref="MasterManager.SetReplicaAckOffset"/>.
    /// </summary>
    public long AckOffset { get; set; }

    /// <summary>
    /// Sends raw bytes to the replica.
    /// Does not update <see cref="Offset"/> — that is the caller's responsibility
    /// so offset tracking stays in one place.
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> data)
    {
        await Socket.SendAsync(data, SocketFlags.None);
    }

    /// <summary>
    /// Sends a REPLCONF GETACK to request the replica's current offset.
    /// Waits until this replica's send offset has reached
    /// <paramref name="replicationLogOffset"/> before issuing the request,
    /// using a backoff-based wait rather than a tight spin.
    /// </summary>
    public async Task GetAckOffsetAsync(
        long replicationLogOffset,
        CancellationToken ct = default)
    {
        // Wait until we've actually sent everything up to the target offset
        // before asking the replica to acknowledge it.
        await WaitForSendOffsetAsync(replicationLogOffset, ct);

        var command = RedisValue.ToBulkStringArray(["REPLCONF", "GETACK", "*"]);

        try
        {
            await Socket.SendAsync(command.Value, SocketFlags.None);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Failed to send GETACK to replica {Id}: {ex.Message}");
        }
    }

    private async Task WaitForSendOffsetAsync(long targetOffset, CancellationToken ct)
    {
        const int initialDelayMs = 1;
        const int maxDelayMs = 50;
        int delayMs = initialDelayMs;

        while (Offset < targetOffset && !ct.IsCancellationRequested)
        {
            await Task.Delay(delayMs, ct);
            delayMs = Math.Min(delayMs * 2, maxDelayMs);
        }
    }
}
