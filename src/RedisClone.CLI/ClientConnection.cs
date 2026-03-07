using System.Net.Sockets;

namespace RedisClone.CLI;

public sealed class ClientConnection(int id, Socket socket)
{
    public int Id { get; } = id;

    public Socket Socket { get; } = socket;
}
