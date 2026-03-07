using RedisClone.CLI.Commands;
using RedisClone.CLI.Logging;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Server.Interfaces;
using RedisClone.CLI.Storage;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Server;

internal sealed class Server(
    AppSettings appSettings, 
    CommandProcessor commandProcessor,
    KvpStorage kvpStorage) : IServer
{
    private const int Backlog = 10;
    private const int BufferSize = 4096;
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
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            listener.Stop();
            kvpStorage.Dispose();
            Console.WriteLine("Server shut down.");
        }
    }

    private async Task HandleConnectionAsync(Socket socket, int connectionId, CancellationToken cancellationToken)
    {
        using (socket)
        {
            var buffer = new byte[BufferSize];

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    RespLogger.Waiting(connectionId);

                    int received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                    if (received == 0)
                    {
                        RespLogger.Disconnected(connectionId);
                        break;
                    }

                    string rawRequest = Encoding.UTF8.GetString(buffer, 0, received);
                    RespLogger.Received(connectionId, rawRequest);

                    RedisValue response = commandProcessor.Process(rawRequest, socket);
                    RespLogger.Sending(connectionId, response.Value);

                    await socket.SendAsync(response.Value, SocketFlags.None, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Connection {connectionId} cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection {connectionId} faulted: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Connection {connectionId} closed.");
            }
        }
    }
}
