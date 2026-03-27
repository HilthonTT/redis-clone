namespace RedisClone.Client.Pooling;

/// <summary>
/// RAII wrapper that returns a connection to the pool on dispose.
/// Use with <c>await using</c>.
/// </summary>
internal readonly struct PooledConnection(RedisConnection connection, ConnectionPool pool) : IAsyncDisposable
{
    public RedisConnection Connection => connection;

    public ValueTask DisposeAsync()
    {
        pool.Return(connection);
        return ValueTask.CompletedTask;
    }
}
