using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class PingHandlerTests : IDisposable
{
    private readonly Ping _handler;
    private readonly Socket _socket;
    private readonly Socket _client;

    public PingHandlerTests()
    {
        _handler = new Ping(AppSettings.Default);
        (_client, _socket) = CommandFactory.CreateSocketPair();
    }

    [Fact]
    public void Handle_WithNoArguments_ReturnsPong()
    {
        var command = CommandFactory.Create(CommandType.Ping);

        var result = _handler.Handle(command, _socket);

        result.Value.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("+PONG\r\n"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _socket.Dispose();
    }
}
