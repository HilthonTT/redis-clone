using RedisClone.Client.Pooling;
using RedisClone.Client.Protocol;
using RedisClone.Client.PubSub;

namespace RedisClone.Client;

/// <summary>
/// A high-level, async-first Redis client with connection pooling.
/// Thread-safe — designed for registration as a singleton in DI containers.
///
/// <code>
/// // Direct usage
/// await using var client = new RedisClient("localhost", 6379);
/// await client.SetAsync("name", "hans");
/// string? name = await client.GetAsync("name");
///
/// // With DI (Blazor / ASP.NET)
/// builder.Services.AddRedisClient(o => o.ConnectionString = "localhost:6379");
/// </code>
/// </summary>
public sealed class RedisClient : IAsyncDisposable
{
    private readonly ConnectionPool _pool;
    private readonly RedisClientOptions _options;
    private bool _disposed;

    public RedisClient(RedisClientOptions options)
    {
        _options = options;
        _pool = new ConnectionPool(options);
    }

    public RedisClient(string host = "localhost", int port = 6379)
        : this(new RedisClientOptions { Host = host, Port = port }) { }

    /// <summary>
    /// Sends a PING to the server. Returns true if the server responds with PONG.
    /// Useful for health checks.
    /// </summary>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["PING"], cancellationToken);
        return result.AsString() == "PONG";
    }

    /// <summary>
    /// GET key — returns the value, or null if the key doesn't exist.
    /// </summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["GET", key], cancellationToken);
        return result.AsString();
    }

    /// <summary>
    /// SET key value — stores a string value.
    /// </summary>
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["SET", key, value], cancellationToken);
        result.ThrowIfError();
    }

    /// <summary>
    /// Asynchronously increments the numeric value stored at the specified key.
    /// </summary>
    /// <remarks>If the key does not exist, it is set to 0 before performing the increment. If the key
    /// contains a value of the wrong type, an error is returned.</remarks>
    /// <param name="key">The key identifying the value to increment. The key must refer to a numeric value.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous increment operation.</returns>
    public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["INCR", key], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// Asynchronously decrements the numeric value stored at the specified key by one.
    /// </summary>
    /// <remarks>If the key does not exist, it is set to 0 before performing the operation. If the key
    /// contains a value of the wrong type or cannot be represented as an integer, an error is returned.</remarks>
    /// <param name="key">The key identifying the value to decrement. The key must refer to a string holding an integer value.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the key after the
    /// decrement operation.</returns>
    public async Task<long> DecrementAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["DECR", key], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// Asynchronously increments the numeric value stored at the specified key by the given delta.
    /// </summary>
    /// <remarks>If the key does not exist, it is set to 0 before performing the operation. If the key
    /// contains a value of the wrong type, an error is returned.</remarks>
    /// <param name="key">The key whose value to increment. Cannot be null or empty.</param>
    /// <param name="delta">The amount by which to increment the value. Can be positive or negative.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the new value after the increment.</returns>
    public async Task<long> IncrementByAsync(
        string key, 
        long delta, 
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["INCRBY", key, delta.ToString()], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// Asynchronously decrements the numeric value stored at the specified key by the given amount.
    /// </summary>
    /// <remarks>If the key does not exist, it is set to 0 before performing the operation. If the key
    /// contains a value of the wrong type, an error is returned.</remarks>
    /// <param name="key">The key identifying the value to decrement. Cannot be null or empty.</param>
    /// <param name="delta">The amount by which to decrement the value. Must be a non-zero integer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the key after the
    /// decrement operation.</returns>
    public async Task<long> DecrementByAsync(
        string key, 
        long delta, 
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["DECRBY", key, delta.ToString()], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// Sends a MULTI command to the server to start a Redis transaction asynchronously.
    /// </summary>
    /// <remarks>This method initiates a transaction block in Redis. Subsequent commands will be queued for
    /// atomic execution until EXEC or DISCARD is called. The method does not return the transaction result; it only
    /// starts the transaction context.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task MultiAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["MULTI"], cancellationToken);
        result.ThrowIfError();
    }

    /// <summary>
    /// Executes the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["EXEC"], cancellationToken);
        result.ThrowIfError();
    }

    /// <summary>
    /// Discards all commands issued after the last transaction started on the current connection asynchronously.
    /// </summary>
    /// <remarks>This method is typically used to abort a transaction and clear any queued commands without
    /// executing them. If the operation is canceled via the provided cancellation token, the transaction state may
    /// remain unchanged.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the discard operation.</param>
    /// <returns>A task that represents the asynchronous discard operation.</returns>
    public async Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["DISCARD"], cancellationToken);
        result.ThrowIfError();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<long> TTLAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["TTL", key], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// SET key value PX milliseconds — stores a string value with an expiry.
    /// </summary>
    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        long ms = (long)expiry.TotalMilliseconds;
        var result = await ExecuteAsync(["SET", key, value, "PX", ms.ToString()], cancellationToken);
        result.ThrowIfError();
    }

    /// <summary>
    /// LPUSH key value [value ...] — prepends values to the head of a list. Returns the list length.
    /// </summary>
    public async Task<long> LPushAsync(string key, params string[] values)
    {
        string[] cmd = ["LPUSH", key, .. values];
        var result = await ExecuteAsync(cmd);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// RPUSH key value [value ...] — appends values to the tail of a list. Returns the list length.
    /// </summary>
    public async Task<long> RPushAsync(string key, params string[] values)
    {
        string[] cmd = ["RPUSH", key, .. values];
        var result = await ExecuteAsync(cmd);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// LPOP key — removes and returns the first element, or null if the list is empty.
    /// </summary>
    public async Task<string?> LPopAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["LPOP", key], cancellationToken);
        return result.ThrowIfError().AsString();
    }

    /// <summary>
    /// LPOP key count — removes and returns up to <paramref name="count"/> elements from the head.
    /// </summary>
    public async Task<List<string?>> LPopAsync(string key, int count, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["LPOP", key, count.ToString()], cancellationToken);
        return result.ThrowIfError().AsStringList();
    }

    /// <summary>
    /// BLPOP key timeout — blocking left pop. Waits up to <paramref name="timeout"/> seconds.
    /// Returns (key, value) or null on timeout.
    /// </summary>
    public async Task<(string Key, string Value)?> BLPopAsync(
        string key, 
        double timeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["BLPOP", key, timeoutSeconds.ToString("F1")], cancellationToken);

        if (result.IsNull)
        {
            return null;
        }

        var arr = result.ThrowIfError().AsArray();
        if (arr.Length < 2)
        {
            return null;
        }

        return (arr[0].AsString()!, arr[1].AsString()!);
    }

    /// <summary>
    /// LLEN key — returns the length of the list, or 0 if the key doesn't exist.
    /// </summary>
    public async Task<long> LLenAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["LLEN", key], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// LRANGE key start end — returns elements in the specified range (inclusive).
    /// Supports negative indices (-1 = last element).
    /// </summary>
    public async Task<List<string?>> LRangeAsync(
        string key,
        int start = 0,
        int end = -1,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["LRANGE", key, start.ToString(), end.ToString()], cancellationToken);
        return result.ThrowIfError().AsStringList();
    }

    /// <summary>
    /// XADD streamKey id field value [field value ...] — appends an entry to a stream.
    /// Use <c>"*"</c> for <paramref name="id"/> to auto-generate the entry ID.
    /// Returns the assigned entry ID.
    /// </summary>
    public async Task<string?> XAddAsync(
        string streamKey, string id, Dictionary<string, string> fields, CancellationToken cancellationToken = default)
    {
        var cmd = new List<string>(4 + fields.Count * 2) { "XADD", streamKey, id };
        foreach (var (field, value) in fields)
        {
            cmd.Add(field);
            cmd.Add(value);
        }

        var result = await ExecuteAsync(cmd.ToArray(), cancellationToken);
        return result.ThrowIfError().AsString();
    }

    /// <summary>
    /// Convenience overload: XADD with auto-generated ID.
    /// </summary>
    public Task<string?> XAddAsync(
        string streamKey, Dictionary<string, string> fields, CancellationToken cancellationToken = default)
        => XAddAsync(streamKey, "*", fields, cancellationToken);

    /// <summary>
    /// TYPE key — returns the type of the value stored at key ("string", "list", "stream", "none").
    /// </summary>
    public async Task<string?> TypeAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["TYPE", key], cancellationToken);
        return result.AsString();
    }

    /// <summary>
    /// KEYS pattern — returns all keys matching the pattern. Only "*" is supported.
    /// </summary>
    public async Task<List<string?>> KeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["KEYS", pattern], cancellationToken);
        return result.ThrowIfError().AsStringList();
    }

    /// <summary>
    /// PUBLISH channel message — publishes a message. Returns the number of subscribers that received it.
    /// </summary>
    public async Task<long> PublishAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["PUBLISH", channel, message], cancellationToken);
        return result.ThrowIfError().AsLong();
    }

    /// <summary>
    /// Creates a new subscriber on a dedicated connection.
    /// The subscriber receives messages as an <see cref="IAsyncEnumerable{T}"/>.
    /// Dispose the subscriber to unsubscribe and release the connection.
    /// </summary>
    public async Task<RedisSubscriber> SubscribeAsync(
        params string[] channels)
    {
        // Pub/sub needs a dedicated connection (not pooled — it stays in subscriber mode)
        var conn = await RedisConnection.ConnectAsync(
            _options.Host,
            _options.Port, 
            _options.ConnectTimeout);

        if (!string.IsNullOrEmpty(_options.Password))
        {
            var authResult = await conn.ExecuteAsync(
                ["AUTH", _options.Username, _options.Password]);
            authResult.ThrowIfError();
        }


        return new RedisSubscriber(conn, channels);
    }

    /// <summary>
    /// ECHO message — returns the message. Useful for testing connectivity.
    /// </summary>
    public async Task<string?> EchoAsync(string message, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["ECHO", message], cancellationToken);
        return result.AsString();
    }

    /// <summary>
    /// CONFIG GET parameter — returns the configuration value.
    /// </summary>
    public async Task<List<string?>> ConfigGetAsync(string parameter, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["CONFIG", "GET", parameter], cancellationToken);
        return result.ThrowIfError().AsStringList();
    }

    /// <summary>
    /// AUTH username password — authenticates the current connection.
    /// Called automatically on every pooled connection when Password is configured.
    /// </summary>
    public async Task AuthAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["AUTH", username, password], cancellationToken);
        result.ThrowIfError();
    }

    public async Task<RespValue> ExecuteAsync(string[] command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var conn = await _pool.RentAsync(cancellationToken);
        try
        {
            return await conn.ExecuteAsync(command, cancellationToken);
        }
        catch (IOException)
        {
            // Connection is dead, don't return it to pool
            await conn.DisposeAsync();
            throw;
        }
        finally
        {
            // Return is safe even after dispose — pool handles it
            _pool.Return(conn);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _pool.DisposeAsync();
    }
}
