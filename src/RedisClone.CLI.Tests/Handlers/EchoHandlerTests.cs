using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class EchoHandlerTests : IAsyncDisposable
{
    private readonly Echo _handler;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public EchoHandlerTests()
    {
        _handler = new Echo(AppSettings.Default);
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    [Fact]
    public void Handle_WithSingle_Argument_ReturnsSimpleString()
    {
        var command = CommandFactory.Create(CommandType.Echo, "hello");
        var result = _handler.Handle(command, _connection);
        result.Value.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("+hello\r\n"));
    }

    [Fact]
    public void Handle_WithNoArguments_ReturnsArgumentError()
    {
        var command = CommandFactory.Create(CommandType.Echo);
        var result = _handler.Handle(command, _connection);
        Encoding.UTF8.GetString(result.Value).Should().StartWith("-ERR wrong number of arguments for 'echo'");
    }

    [Fact]
    public void Handle_WithMultipleArguments_ReturnsFirstArgument()
    {
        var command = CommandFactory.Create(CommandType.Echo, "hello", "world");
        var result = _handler.Handle(command, _connection);
        Encoding.UTF8.GetString(result.Value).Should().Be("+hello\r\n");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}