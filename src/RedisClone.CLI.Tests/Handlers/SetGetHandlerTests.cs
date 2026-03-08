using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class SetGetHandlerTests : IDisposable
{
    private readonly Set _setHandler;
    private readonly Get _getHandler;
    private readonly Socket _socket;
    private readonly Socket _client;

    public SetGetHandlerTests()
    {
        var storage = new KvpStorage();
        var settings = AppSettings.Default;
        _setHandler = new Set(storage, settings);
        _getHandler = new Get(storage, settings);
        (_client, _socket) = CommandFactory.CreateSocketPair();
    }

    [Fact]
    public void Set_WithKeyAndValue_ReturnsOk()
    {
        var command = CommandFactory.Create(CommandType.Set, "name", "hilthon");

        var result = _setHandler.Handle(command, _socket);

        Encoding.UTF8.GetString(result.Value).Should().Be("+OK\r\n");
    }

    [Fact]
    public void Set_WithOnlyKey_ReturnsArgumentError()
    {
        // min is 2, so just a key should fail
        var command = CommandFactory.Create(CommandType.Set, "key");

        var result = _setHandler.Handle(command, _socket);

        Encoding.UTF8.GetString(result.Value).Should().StartWith("-ERR wrong number of arguments for 'set'");
    }

    [Fact]
    public void Set_WithPxExpiry_ReturnsOk()
    {
        var command = CommandFactory.Create(CommandType.Set, "key", "value", "PX", "5000");

        var result = _setHandler.Handle(command, _socket);

        Encoding.UTF8.GetString(result.Value).Should().Be("+OK\r\n");
    }

    [Fact]
    public void Set_WithPxFlagButNoMs_ReturnsArgumentError()
    {
        // max is 4, so 5 args should fail at the argument validation level
        var command = CommandFactory.Create(CommandType.Set, "key", "value", "PX", "5000", "extra");

        var result = _setHandler.Handle(command, _socket);

        Encoding.UTF8.GetString(result.Value).Should().StartWith("-ERR wrong number of arguments for 'set'");
    }

    [Fact]
    public void Get_AfterSet_ReturnsBulkString()
    {
        string value = "hilthon";

        _setHandler.Handle(CommandFactory.Create(CommandType.Set, "name", value), _socket);

        var result = _getHandler.Handle(CommandFactory.Create(CommandType.Get, "name"), _socket);

        Encoding.UTF8.GetString(result.Value).Should().Be($"${value.Length}\r\n{value}\r\n");
    }

    [Fact]
    public void Get_MissingKey_ReturnsNullBulkString()
    {
        var result = _getHandler.Handle(CommandFactory.Create(CommandType.Get, "missing"), _socket);

        Encoding.UTF8.GetString(result.Value).Should().Be("$-1\r\n");
    }

    [Fact]
    public void Get_WithNoArguments_ReturnsArgumentError()
    {
        var command = CommandFactory.Create(CommandType.Get);

        var result = _getHandler.Handle(command, _socket);

        Encoding.UTF8.GetString(result.Value).Should().StartWith("-ERR wrong number of arguments for 'get'");
    }

    [Fact]
    public void Get_AfterSetWithExpiry_ReturnsValue()
    {
        // Value should still be retrievable immediately after setting with PX
        _setHandler.Handle(CommandFactory.Create(CommandType.Set, "temp", "data", "PX", "60000"), _socket);

        var result = _getHandler.Handle(CommandFactory.Create(CommandType.Get, "temp"), _socket);

        Encoding.UTF8.GetString(result.Value).Should().Be("$4\r\ndata\r\n");
    }

    public void Dispose()
    {
        _socket.Dispose();
        _client.Dispose();
    }
}