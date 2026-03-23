using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class XAddHandlerTests : IAsyncDisposable
{
    private readonly StreamStorage _storage = new();
    private readonly AppSettings _settings = AppSettings.Default;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public XAddHandlerTests()
    {
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(CLI.Models.RedisValue v) => Encoding.UTF8.GetString(v.Value);

    [Fact]
    public void XAdd_ExplicitId_ReturnsBulkStringId()
    {
        var handler = new XAdd(_settings, _storage);
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "1-0", "name", "hans");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$3\r\n1-0\r\n");
    }

    [Fact]
    public void XAdd_AutoId_ReturnsGeneratedId()
    {
        var handler = new XAdd(_settings, _storage);
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "*", "name", "hans");
        var result = handler.Handle(cmd, _connection);

        var decoded = Decode(result);
        decoded.Should().StartWith("$");
        decoded.Should().Contain("-");
    }

    [Fact]
    public void XAdd_InvalidId_ReturnsError()
    {
        var handler = new XAdd(_settings, _storage);
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "0-0", "name", "hans");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("ERR");
    }

    [Fact]
    public void XAdd_OddFieldCount_ReturnsError()
    {
        var handler = new XAdd(_settings, _storage);
        // 3 field args (odd) → not valid key-value pairs
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "1-0", "name", "hans", "extra");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("ERR wrong number of arguments");
    }

    [Fact]
    public void XAdd_TooFewArgs_ReturnsError()
    {
        var handler = new XAdd(_settings, _storage);
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "1-0", "name");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    [Fact]
    public void XAdd_MultipleFields_Succeeds()
    {
        var handler = new XAdd(_settings, _storage);
        var cmd = CommandFactory.Create(CommandType.XAdd, "mystream", "1-0",
            "name", "hans", "age", "25");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$3\r\n1-0\r\n");
    }

    [Fact]
    public void XAdd_SequentialIds_AllSucceed()
    {
        var handler = new XAdd(_settings, _storage);

        handler.Handle(
            CommandFactory.Create(CommandType.XAdd, "s", "1-0", "k", "v1"), _connection);
        handler.Handle(
            CommandFactory.Create(CommandType.XAdd, "s", "1-1", "k", "v2"), _connection);
        var result = handler.Handle(
            CommandFactory.Create(CommandType.XAdd, "s", "2-0", "k", "v3"), _connection);

        Decode(result).Should().Be("$3\r\n2-0\r\n");
    }

    [Fact]
    public void XAdd_DecreasingId_ReturnsError()
    {
        var handler = new XAdd(_settings, _storage);
        handler.Handle(
            CommandFactory.Create(CommandType.XAdd, "s", "5-0", "k", "v1"), _connection);

        var result = handler.Handle(
            CommandFactory.Create(CommandType.XAdd, "s", "3-0", "k", "v2"), _connection);

        Decode(result).Should().Contain("equal or smaller");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}