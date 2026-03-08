using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class EchoHandlerTests : IDisposable
{
    private readonly Echo _handler;
    private readonly Socket _socket;
    private readonly Socket _client;

    public EchoHandlerTests()
    {
        _handler = new Echo(AppSettings.Default);
        (_client, _socket) = CommandFactory.CreateSocketPair();
    }

    [Fact]
    public void Handle_WithSingle_Argument_ReturnsSimpleString()
    {
        var command = CommandFactory.Create(CommandType.Echo, "hello");

        var result = _handler.Handle(command, _socket);

        result.Value.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("+hello\r\n"));
    }

    [Fact]
    public void Handle_WithNoArguments_ReturnsArgumentError()
    {
        var command = CommandFactory.Create(CommandType.Echo);

        var result = _handler.Handle(command, _socket);

        var response = Encoding.UTF8.GetString(result.Value);
        response.Should().StartWith("-ERR wrong number of arguments for 'echo'");
    }

    [Fact]
    public void Handle_WithMultipleArguments_ReturnsFirstArgument()
    {
        var command = CommandFactory.Create(CommandType.Echo, "hello", "world");

        var result = _handler.Handle(command, _socket);

        var response = Encoding.UTF8.GetString(result.Value);
        response.Should().Be("+hello\r\n");
    }

    public void Dispose()
    {
        _socket.Dispose();
        _client.Dispose();
    }
}
