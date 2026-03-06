using RedisClone.CLI.Extensions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Server;

internal sealed class Server
{
    private const int Port = 6379;
    private const int Backlog = 10;
    private const int BufferSize = 1024;
    private int _nextId = 0;

    internal async Task StartAndListenAsync(CancellationToken cancellationToken = default)
    {
        using var server = new TcpListener(IPAddress.Any, Port);

        try
        {
            server.Start(Backlog);
            Console.WriteLine($"Server listening on port {Port}");

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket socket = await server.AcceptSocketAsync(cancellationToken);
                Console.WriteLine("Connected new client!");
                _ = Task.Run(
                    () => HandleConnectionAsync(socket, cancellationToken),
                    cancellationToken
                ).ContinueWith(t => Console.WriteLine($"Connection task faulted: {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown, not an error
        }
        finally
        {
            Console.WriteLine("Server shutting down...");
            server.Stop();
        }
    }

    private async Task HandleConnectionAsync(Socket socket, CancellationToken cancellationToken)
    {
        int connectionId = Interlocked.Increment(ref _nextId);
        using (socket)
        {
            try
            {
                var buffer = new byte[BufferSize];

                while (socket.Connected && !cancellationToken.IsCancellationRequested)
                {
                    $"Connection Id {connectionId}. Waiting for request...".WriteLineEncoded();

                    int received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                    if (received == 0)
                    {
                        $"Connection Id {connectionId}. Client disconnected gracefully.".WriteLineEncoded();
                        break;
                    }

                    var requestPayload = Encoding.UTF8.GetString(buffer, 0, received);
                    $"Connection Id {connectionId}. Received: {requestPayload}".WriteLineEncoded();

                    byte[] response = Encoding.UTF8.GetBytes("+OK\r\n");
                    $"Connection Id {connectionId}. Sending: {Encoding.UTF8.GetString(response)}".WriteLineEncoded();

                    await socket.SendAsync(response, SocketFlags.None, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Connection Id {connectionId}. Cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection Id {connectionId}. Error: {ex.Message}");
            }
        }

        Console.WriteLine($"Connection Id {connectionId}. Closed.");
    }
}
