using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace RedisClone.CLI.Replication;

public sealed class ReplicaClient : IAsyncDisposable
{
    private const int BufferSize = 4096;

    private readonly TcpClient _tcpClient;
    private readonly Socket _socket;
    private readonly Channel<byte[]> _commandChannel = 
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    internal ClientConnection ClientConnection { get; }

    // Public reader so consumers can await commands without exposing the writer.
    internal ChannelReader<byte[]> CommandReader => _commandChannel.Reader;

    public ReplicaClient(AppSettings settings)
    {
        SlaveReplicaSettings rep = settings.Replication.SlaveReplicaSettings
            ?? throw new InvalidOperationException("Slave replication settings are not configured.");

        _tcpClient = new TcpClient(rep.MasterHost, rep.MasterPort);
        _socket = _tcpClient.Client;
        ClientConnection = new ClientConnection(-1, _socket);
    }

    public Task PingAsync() => SendAndReceiveAsync(RedisValue.ToBulkStringArray(["PING"]));

    public Task ConfListeningPortAsync(int port) =>
        SendWithConfirmationAsync(RedisValue.ToBulkStringArray(["REPLCONF", "listening-port", port.ToString()]));

    public Task ConfCapabilitiesAsync() =>
        SendWithConfirmationAsync(RedisValue.ToBulkStringArray(["REPLCONF", "capa", "psync2"]));

    public async Task PSyncAsync(string masterReplicationId, long offset)
    {
        var message = RedisValue.ToBulkStringArray(["PSYNC", masterReplicationId, offset.ToString()]);
        var payload = await SendAndReceiveAsync(message);
        EnqueueCommandsFromPSyncPayload(payload);
    }

    public async Task SendAckAsync(long offset)
    {
        RedisValue message = RedisValue.ToBulkStringArray(["REPLCONF", "ACK", offset.ToString()]);
        await _socket.SendAsync(message.Value, SocketFlags.None);
    }

    public async Task WaitForCommandsAsync(CancellationToken cancellationToken = default)
    {
        // Use a pooled, resizable buffer rather than a fixed 1024-byte array.
        using var buffer = MemoryPool<byte>.Shared.Rent(BufferSize);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
            {
                int received = await _socket.ReceiveAsync(buffer.Memory, SocketFlags.None, cancellationToken);
                if (received == 0)
                {
                    break; // graceful close
                }

                EnqueueRawCommands(buffer.Memory.Span[..received]);
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        finally
        {
            _commandChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Parses the PSYNC response which may contain:
    ///   1. A simple string response   (+FULLRESYNC ... \r\n)
    ///   2. An optional RDB file       ($&lt;len&gt;\r\n&lt;binary&gt;)
    ///   3. Zero or more commands      (* ...)
    /// We work on the raw bytes to avoid corrupting binary RDB content.
    /// </summary>
    private void EnqueueCommandsFromPSyncPayload(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;

        // Skip past the simple-string response line (+...\r\n).
        int cursor = SkipSimpleString(span);

        // Skip past the RDB bulk string ($<len>\r\n<bytes>) if present.
        if (cursor < span.Length && span[cursor] == (byte)'$')
        {
            cursor = SkipRdbBulkString(span, cursor);
        }

        // Remaining bytes are replication commands — enqueue them.
        if (cursor < span.Length)
        {
            EnqueueRawCommands(span[cursor..]);
        }    
    }

    private static int SkipSimpleString(ReadOnlySpan<byte> span)
    {
        int i = 0;
        while (i < span.Length - 1)
        {
            if (span[i] == '\r' && span[i + 1] == '\n')
            {
                return i + 2;
            }

            i++;
        }
        return span.Length;
    }

    private static int SkipRdbBulkString(ReadOnlySpan<byte> span, int start)
    {
        // Format: $<decimal-length>\r\n<binary>
        int i = start + 1; // skip '$'
        int lenStart = i;

        while (i < span.Length && span[i] != '\r')
        {
            i++;
        }

        var lengthText = Encoding.ASCII.GetString(span[lenStart..i]);
        if (!int.TryParse(lengthText, out int rdbLength))
        {
            return i;
        }

        return i + 2 + rdbLength; // skip \r\n and then the binary payload
    }

    /// <summary>
    /// Splits a raw byte span into individual RESP array commands (starting with '*')
    /// and writes them to the channel. Works on bytes to avoid misinterpreting binary data.
    /// </summary>
    private void EnqueueRawCommands(ReadOnlySpan<byte> span)
    {
        int start = 1;

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == (byte)'*')
            {
                if (start >= 0)
                {
                    Enqueue(span[start..]);
                }
            }
        }

        void Enqueue(ReadOnlySpan<byte> command)
        {
            if (!command.IsEmpty)
            {
                _commandChannel.Writer.TryWrite(command.ToArray());
            }
        }
    }

    private async Task<ReadOnlyMemory<byte>> SendAndReceiveAsync(RedisValue message)
    {
        if (!_tcpClient.Connected)
        {
            throw new InvalidOperationException("Not connected to master.");
        }

        await _socket.SendAsync(message.Value, SocketFlags.None);

        using var buffer = MemoryPool<byte>.Shared.Rent(BufferSize);
        int received = await _socket.ReceiveAsync(buffer.Memory, SocketFlags.None);

        // Copy out of pooled memory before returning.
        return buffer.Memory[..received].ToArray();
    }

    private async Task SendWithConfirmationAsync(RedisValue message, string expected = RedisValue.OkValue)
    {
        var responseBytes = await SendAndReceiveAsync(message);
        var response = Encoding.UTF8.GetString(responseBytes.Span);

        if (response != expected)
        {
            throw new IOException($"Expected '{expected}' from master, got: '{response}'");
        }
    }

    public ValueTask DisposeAsync()
    {
        _commandChannel.Writer.TryComplete();
        _tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}
