namespace RedisClone.CLI.Server.Interfaces;

public interface IServer
{
    Task StartAndListenAsync(CancellationToken cancellationToken = default);
}
