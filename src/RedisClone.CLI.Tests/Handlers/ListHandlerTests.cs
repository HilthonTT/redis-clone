using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class ListHandlerTests : IAsyncDisposable
{
    private readonly ListStorage _listStorage = new();
    private readonly AppSettings _settings = AppSettings.Default;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public ListHandlerTests()
    {
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(CLI.Models.RedisValue v) => Encoding.UTF8.GetString(v.Value);

    // ─── LPUSH ──────────────────────────────────────────────

    [Fact]
    public void LPush_SingleValue_ReturnsCount()
    {
        var handler = new LPush(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPush, "mylist", "a");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":1\r\n");
    }

    [Fact]
    public void LPush_MultipleValues_ReturnsAccumulatedCount()
    {
        var handler = new LPush(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPush, "mylist", "a", "b", "c");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":3\r\n");
    }

    [Fact]
    public void LPush_TooFewArgs_ReturnsError()
    {
        var handler = new LPush(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPush, "mylist");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    // ─── RPUSH ──────────────────────────────────────────────

    [Fact]
    public void RPush_SingleValue_ReturnsCount()
    {
        var handler = new RPush(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.RPush, "mylist", "a");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":1\r\n");
    }

    [Fact]
    public void RPush_MultipleValues_ReturnsCount()
    {
        var handler = new RPush(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.RPush, "mylist", "x", "y");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":2\r\n");
    }

    // ─── LLEN ───────────────────────────────────────────────

    [Fact]
    public void LLen_ExistingList_ReturnsCount()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c"]);

        var handler = new LLen(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LLen, "mylist");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":3\r\n");
    }

    [Fact]
    public void LLen_MissingKey_ReturnsZero()
    {
        var handler = new LLen(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LLen, "missing");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be(":0\r\n");
    }

    [Fact]
    public void LLen_TooManyArgs_ReturnsError()
    {
        var handler = new LLen(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LLen, "a", "b");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    // ─── LRANGE ─────────────────────────────────────────────

    [Fact]
    public void LRange_FullRange_ReturnsAllElements()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c"]);

        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "0", "-1");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("a").And.Contain("b").And.Contain("c");
    }

    [Fact]
    public void LRange_PartialRange_ReturnsSlice()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c", "d", "e"]);

        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "0", "1");
        var result = handler.Handle(cmd, _connection);
        var decoded = Decode(result);

        decoded.Should().Contain("a").And.Contain("b");
        decoded.Should().NotContain("$1\r\nc\r\n");
    }

    [Fact]
    public void LRange_NegativeIndices_ReturnsLastElements()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c", "d", "e"]);

        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "-2", "-1");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("d").And.Contain("e");
    }

    [Fact]
    public void LRange_StartAfterEnd_ReturnsEmpty()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c"]);

        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "3", "1");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$*0\r\n");
    }

    [Fact]
    public void LRange_MissingKey_ReturnsEmptyArray()
    {
        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "missing", "0", "-1");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$*0\r\n");
    }

    [Fact]
    public void LRange_NonIntegerIndex_ReturnsError()
    {
        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "0", "abc");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("ERR value is not an integer");
    }

    [Fact]
    public void LRange_TooFewArgs_ReturnsError()
    {
        var handler = new LRange(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LRange, "mylist", "0");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    // ─── LPOP ───────────────────────────────────────────────

    [Fact]
    public void LPop_ExistingList_ReturnsFirstElement()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c"]);

        var handler = new LLPop(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPop, "mylist");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$1\r\na\r\n");
    }

    [Fact]
    public void LPop_WithCount_ReturnsBulkArray()
    {
        _listStorage.AddLast("mylist", ["a", "b", "c"]);

        var handler = new LLPop(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPop, "mylist", "2");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public void LPop_EmptyList_ReturnsEmptyArray()
    {
        var handler = new LLPop(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPop, "missing");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Be("$*0\r\n");
    }

    [Fact]
    public void LPop_NonIntegerCount_ReturnsError()
    {
        _listStorage.AddLast("mylist", ["a"]);

        var handler = new LLPop(_settings, _listStorage);
        var cmd = CommandFactory.Create(CommandType.LPop, "mylist", "abc");
        var result = handler.Handle(cmd, _connection);

        Decode(result).Should().Contain("ERR value is not an integer");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
