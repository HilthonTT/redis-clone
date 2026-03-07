using RedisClone.CLI.Commands;
using RedisClone.CLI.Logging;
using RedisClone.CLI.Models;
using RedisClone.CLI.Server.Interfaces;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Server;

internal sealed class TcpConnectionWorker(CommandProcessor commandProcessor) : IWorker
{
    private const int BufferSize = 4096;

    public async Task HandleConnectionAsync(ClientConnection connection, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[BufferSize];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RespLogger.Waiting(connection.Id);

                int received = await connection.Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (received == 0)
                {
                    RespLogger.Disconnected(connection.Id);
                    break;
                }

                string rawRequest = Encoding.UTF8.GetString(buffer, 0, received);
                RespLogger.Received(connection.Id, rawRequest);

                RedisValue response = commandProcessor.Process(rawRequest, connection.Socket);
                RespLogger.Sending(connection.Id, response.Value);

                await connection.Socket.SendAsync(response.Value, SocketFlags.None, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Connection {connection.Id} cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection {connection.Id} faulted: {ex.Message}");
        }
    }
}
