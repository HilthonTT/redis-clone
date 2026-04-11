using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Options;
using RedisClone.CLI.Security;
using RedisClone.CLI.Server.Interfaces;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace RedisClone.CLI.Server;

internal sealed class Server(
    AppSettings appSettings,
    AppMetrics.AppMetrics appMetrics,
    IServiceProvider serviceProvider,
    IpGuard ipGuard) : IServer
{
    private int _totalConnections;

    private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
    private const int Backlog = 10;
    private int _connectionIdSeed;

    public async Task StartAndListenAsync(CancellationToken cancellationToken = default)
    {
        using var listener = new TcpListener(IPAddress.Any, appSettings.Runtime.Port);
        try
        {
            listener.Start(Backlog);
            Console.WriteLine($"Server listening on port {appSettings.Runtime.Port}");

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket socket = await listener.AcceptSocketAsync(cancellationToken);
                int connectionId = Interlocked.Increment(ref _connectionIdSeed);
                Console.WriteLine($"Connection {connectionId} accepted.");
                _ = HandleConnectionAsync(socket, connectionId, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            listener.Stop();
            Console.WriteLine("Server shut down.");
        }
    }

    private async Task HandleConnectionAsync(Socket socket, int connectionId, CancellationToken cancellationToken)
    {
        string ip = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Unknown";

        // Global cap
        if (Interlocked.Increment(ref _totalConnections) > appSettings.Security.MaxTotalConnections)
        {
            Interlocked.Decrement(ref _totalConnections);
            socket.Close();
            Console.WriteLine($"[Guard] Rejected {ip} — global connection cap reached.");
            return;
        }

        // per-IP connection cap
        if (!ipGuard.TryAcceptConnection(ip))
        {
            Interlocked.Decrement(ref _totalConnections);
            socket.Close();
            Console.WriteLine($"[Guard] Rejected {ip} — per-IP connection limit.");
            return;
        }

        appMetrics.ConnectionsTotal.Inc();
        appMetrics.ActiveConnections.Inc();

        using (socket)
        {
            var connection = new ClientConnection(connectionId, socket);
            bool requiresAuth = !string.IsNullOrEmpty(appSettings.Security.RequirePass);
            connection.SetInitialAuthState(requiresAuth);

            _clients.TryAdd(connectionId, connection);
            try
            {
                var worker = serviceProvider.GetRequiredService<IWorker>();
                await worker.HandleConnectionAsync(connection, cancellationToken);
            }
            finally
            {
                _clients.TryRemove(connectionId, out _);
                ipGuard.ReleaseConnection(ip);
                Interlocked.Decrement(ref _totalConnections);
                appMetrics.ActiveConnections.Dec();
                Console.WriteLine($"Connection {connectionId} closed.");
            }
        }
    }
}
