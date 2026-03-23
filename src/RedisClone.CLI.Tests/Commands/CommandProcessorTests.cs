using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Commands;

public sealed class CommandProcessorTests : IAsyncDisposable
{
    private readonly CommandProcessor _processor;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public CommandProcessorTests()
    {
        var settings = AppSettings.Default;
        var kvp = new KvpStorage();

        ICommandHandler[] handlers =
        [
            new Ping(settings),
            new Echo(settings),
            new Get(kvp, settings),
            new Set(kvp, settings),
        ];

        _processor = new CommandProcessor(handlers);
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(CLI.Models.RedisValue v) => Encoding.UTF8.GetString(v.Value);

    [Fact]
    public async Task Process_PingCommand_ReturnsPong()
    {
        var result = await _processor.Process("*1\r\n$4\r\nPING\r\n", _connection);
        Decode(result).Should().Be("+PONG\r\n");
    }

    [Fact]
    public async Task Process_EchoCommand_ReturnsArgument()
    {
        var result = await _processor.Process("*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n", _connection);
        Decode(result).Should().Be("+hello\r\n");
    }

    [Fact]
    public async Task Process_SetThenGet_ReturnsStoredValue()
    {
        await _processor.Process("*3\r\n$3\r\nSET\r\n$4\r\nname\r\n$4\r\nhans\r\n", _connection);
        var result = await _processor.Process("*2\r\n$3\r\nGET\r\n$4\r\nname\r\n", _connection);

        Decode(result).Should().Be("$4\r\nhans\r\n");
    }

    [Fact]
    public async Task Process_UnknownCommand_ReturnsError()
    {
        var result = await _processor.Process("*1\r\n$7\r\nINVALID\r\n", _connection);
        Decode(result).Should().Contain("Unknown command");
    }

    [Fact]
    public async Task Process_EmptyPayload_ReturnsError()
    {
        var result = await _processor.Process("", _connection);
        Decode(result).Should().Contain("Unknown command");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
