using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class PingHandlerTests : IAsyncDisposable
{
    private readonly Ping _handler;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public PingHandlerTests()
    {
        _handler = new Ping(AppSettings.Default);
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    [Fact]
    public void Handle_WithNoArguments_ReturnsPong()
    {
        var command = CommandFactory.Create(CommandType.Ping);
        var result = _handler.Handle(command, _connection);
        result.Value.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("+PONG\r\n"));
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
