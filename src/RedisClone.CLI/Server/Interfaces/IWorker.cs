namespace RedisClone.CLI.Server.Interfaces;

public interface IWorker
{
    Task HandleConnectionAsync(ClientConnection connection, CancellationToken cancellationToken = default);
}

