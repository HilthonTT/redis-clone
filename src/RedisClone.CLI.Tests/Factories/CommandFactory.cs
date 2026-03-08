using RedisClone.CLI.Commands;
using System.Net;
using System.Net.Sockets;

namespace RedisClone.CLI.Tests.Factories;

internal static class CommandFactory
{
    internal static (Socket client, Socket server) CreateSocketPair()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var server = listener.AcceptSocket();
        listener.Stop();
        return (client, server);
    }

    internal static Command Create(CommandType type, params string[] args) => new(type, args);
}
