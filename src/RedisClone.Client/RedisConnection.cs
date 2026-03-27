using RedisClone.Client.Exceptions;
using RedisClone.Client.Protocol;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace RedisClone.Client;

/// <summary>
/// A single TCP connection to a Redis server with RESP protocol framing.
/// Not thread-safe — use one connection per concurrent operation.
/// </summary>
public sealed class RedisConnection : IAsyncDisposable
{
    private readonly TcpClient _tcp;
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private bool _disposed;

    private RedisConnection(TcpClient tcp, PipeReader reader, PipeWriter writer)
    {
        _tcp = tcp;
        _reader = reader;
        _writer = writer;
    }

    public bool IsConnected => _tcp.Connected && !_disposed;

    /// <summary>
    /// Opens a new connection to the specified endpoint.
    /// </summary>
    public static async Task<RedisConnection> ConnectAsync(
        string host, 
        int port, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        var tcp = new TcpClient
        {
            NoDelay = true,
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await tcp.ConnectAsync(host, port, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            tcp.Dispose();
            throw new RedisConnectionException(
                $"Timed out connecting to {host}:{port} after {timeout.TotalSeconds:F1}s.");
        }

        NetworkStream stream = tcp.GetStream();
        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));

        return new RedisConnection(tcp, reader, writer);
    }

    /// <summary>
    /// Sends a command and reads a single RESP response.
    /// </summary>
    public async Task<RespValue> ExecuteAsync(string[] command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ReadOnlyMemory<byte> payload = RespWriter.Encode(command);

        // Write
        var result = await _writer.WriteAsync(payload, cancellationToken);
        if (result.IsCompleted)
        {
            throw new IOException("Connection closed during write.");
        }

        // Read one complete RESP value
        return await RespReader.ReadAsync(_reader, cancellationToken);
    }

    /// <summary>
    /// Sends a command without waiting for a response.
    /// Used for fire-and-forget operations like subscribing.
    /// </summary>
    public async Task SendAsync(string[] command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReadOnlyMemory<byte> payload = RespWriter.Encode(command);
        await _writer.WriteAsync(payload, cancellationToken);
    }

    /// <summary>
    /// Reads a single RESP value from the connection without sending a command first.
    /// Used for receiving push messages (pub/sub).
    /// </summary>
    public Task<RespValue> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return RespReader.ReadAsync(_reader, cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteAsync(["PING"], cancellationToken);
            return result.AsString() == "PONG";
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await _reader.CompleteAsync();
        await _writer.CompleteAsync();
        _tcp?.Dispose();
    }
}
