namespace RedisClone.CLI.Server.Interfaces;

internal interface IWorker
{
    Task HandleConnectionAsync(ClientConnection connection, CancellationToken cancellationToken = default);
}

