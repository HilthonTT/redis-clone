using RedisClone.CLI.Commands;
using RedisClone.CLI.Logging;
using RedisClone.CLI.Models;
using RedisClone.CLI.Protocol;
using RedisClone.CLI.Server.Interfaces;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace RedisClone.CLI.Server;

internal sealed class TcpConnectionWorker(CommandProcessor commandProcessor) : IWorker
{
    public async Task HandleConnectionAsync(
        ClientConnection connection, 
        CancellationToken cancellationToken = default)
    {
        var stream = new NetworkStream(connection.Socket, ownsSocket: false);
        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RespLogger.Waiting(connection.Id);

                RespResult? parsed = await RespParser.ReadAsync(reader, cancellationToken);
                if (parsed is null)
                {
                    RespLogger.Disconnected(connection.Id);
                    break;
                }

                var command = Command.FromResp(parsed);
                RespLogger.Received(connection.Id, $"{command.Type} {string.Join(' ', command.Arguments)}");

                RedisValue response = await commandProcessor.ProcessCommand(command, connection);
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
        finally
        {
            await reader.CompleteAsync();
            stream.Dispose();
        }
    }
}
