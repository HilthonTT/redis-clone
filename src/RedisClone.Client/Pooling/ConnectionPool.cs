using RedisClone.Client.Exceptions;
using System.Collections.Concurrent;

namespace RedisClone.Client.Pooling;

/// <summary>
/// A bounded async connection pool. Connections are created lazily and recycled.
/// Dead connections are discarded and replaced on next lease.
/// </summary>
public sealed class ConnectionPool : IAsyncDisposable
{
    private readonly RedisClientOptions _options;
    private readonly SemaphoreSlim _gate;
    private readonly ConcurrentBag<RedisConnection> _idle = [];
    private int _totalCreated;
    private bool _disposed;

    public ConnectionPool(RedisClientOptions options)
    {
        _options = options;
        _gate = new SemaphoreSlim(options.PoolSize, options.PoolSize);
    }

    /// <summary>
    /// Leases a connection from the pool. The caller must return it via <see cref="Return"/>.
    /// Creates a new connection if none are idle and the pool isn't full.
    /// </summary>
    public async Task<RedisConnection> RentAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _gate.WaitAsync(_options.PoolTimeout, cancellationToken))
        {
            throw new RedisConnectionException(
                $"Timed out waiting for a pooled connection after {_options.PoolTimeout.TotalSeconds:F1}s. " +
                $"Pool size: {_options.PoolSize}. Consider increasing PoolSize.");
        }

        // Try to reuse an idle connection
        while (_idle.TryTake(out RedisConnection? existing))
        {
            if (existing.IsConnected)
            {
                return existing;
            }

            // Dead connection — discard and decrement total
            await existing.DisposeAsync();
            Interlocked.Decrement(ref _totalCreated);
        }

        // No idle connections — create a new one
        try
        {
            var conn = await RedisConnection.ConnectAsync(
                _options.Host,
                _options.Port, 
                _options.ConnectTimeout, 
                cancellationToken);

            Interlocked.Increment(ref _totalCreated);
            return conn;
        }
        catch
        {
            _gate.Release(); // Give the slot back
            throw;
        }
    }

    /// <summary>
    /// Returns a connection to the pool for reuse.
    /// If the connection is dead, it is disposed instead.
    /// </summary>
    public void Return(RedisConnection connection)
    {
        if (_disposed || !connection.IsConnected)
        {
            _ = connection.DisposeAsync();
            Interlocked.Decrement(ref _totalCreated);
        }
        else
        {
            _idle.Add(connection);
        }

        _gate.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        while (_idle.TryTake(out var conn))
        {
            await conn.DisposeAsync();
        }

        _gate.Dispose();
    }
}
