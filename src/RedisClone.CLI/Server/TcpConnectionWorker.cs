using RedisClone.CLI.Commands;
using RedisClone.CLI.Logging;
using RedisClone.CLI.Models;
using RedisClone.CLI.Protocol;
using RedisClone.CLI.Server.Interfaces;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace RedisClone.CLI.Server;

internal sealed class TcpConnectionWorker(
    CommandProcessor commandProcessor, 
    AppMetrics.AppMetrics metrics) : IWorker
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
                var commandName = command.Type.ToString().ToLowerInvariant();

                var stopwatch = Stopwatch.StartNew();

                try
                {
                    RespLogger.Received(connection.Id, $"{command.Type} {string.Join(' ', command.Arguments)}");

                    RedisValue response = await commandProcessor.ProcessCommand(command, connection);

                    stopwatch.Stop();
                    RespLogger.Sending(connection.Id, response.Value);

                    metrics.CommandDurationSeconds
                    .WithLabels(commandName)
                    .Observe(stopwatch.Elapsed.TotalSeconds);

                    metrics.CommandsTotal
                        .WithLabels(commandName, "success")
                        .Inc();

                    await connection.Socket.SendAsync(response.Value, SocketFlags.None, cancellationToken);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    metrics.CommandDurationSeconds
                        .WithLabels(commandName)
                        .Observe(stopwatch.Elapsed.TotalSeconds);

                    metrics.CommandsTotal
                        .WithLabels(commandName, "error")
                        .Inc();

                    metrics.CommandErrorsTotal
                        .WithLabels(commandName, ex.GetType().Name)
                        .Inc();

                    throw;
                }
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
