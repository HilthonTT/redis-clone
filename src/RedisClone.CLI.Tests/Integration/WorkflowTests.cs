using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Integration;

/// <summary>
/// End-to-end tests that exercise multiple handlers through the CommandProcessor,
/// verifying real Redis-like command sequences work correctly.
/// </summary>
public sealed class WorkflowTests : IAsyncDisposable
{
    private readonly CommandProcessor _processor;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public WorkflowTests()
    {
        var settings = AppSettings.Default;
        var kvp = new KvpStorage();
        var list = new ListStorage();
        var stream = new StreamStorage();
        var storageManager = new StorageManager(kvp, list, stream);
        var pubSub = new PubSub();

        ICommandHandler[] handlers =
        [
            new Ping(settings),
            new Echo(settings),
            new Get(kvp, settings),
            new Set(kvp, settings),
            new LPush(settings, list, pubSub),
            new RPush(settings, list, pubSub),
            new LLen(settings, list),
            new LRange(settings, list),
            new LLPop(settings, list),
            new Keys(storageManager, settings),
            new CLI.Commands.Handlers.Type(storageManager, settings),
            new XAdd(settings, stream, pubSub),
            new Publish(settings, pubSub),
            new Config(settings),
        ];

        _processor = new CommandProcessor(handlers);
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(CLI.Models.RedisValue v) => Encoding.UTF8.GetString(v.Value);

    private static string Resp(params string[] parts)
    {
        var sb = new StringBuilder();
        sb.Append($"*{parts.Length}\r\n");
        foreach (var part in parts)
        {
            sb.Append($"${part.Length}\r\n{part}\r\n");
        }
        return sb.ToString();
    }

    // ─── String workflow ────────────────────────────────────

    [Fact]
    public async Task SetGetDelete_Workflow()
    {
        // SET
        var r1 = await _processor.Process(Resp("SET", "user", "hans"), _connection);
        Decode(r1).Should().Be("+OK\r\n");

        // GET
        var r2 = await _processor.Process(Resp("GET", "user"), _connection);
        Decode(r2).Should().Be("$4\r\nhans\r\n");

        // TYPE
        var r3 = await _processor.Process(Resp("TYPE", "user"), _connection);
        Decode(r3).Should().Be("+string\r\n");

        // KEYS *
        var r4 = await _processor.Process(Resp("KEYS", "*"), _connection);
        Decode(r4).Should().Contain("user");

        // Overwrite
        await _processor.Process(Resp("SET", "user", "lena"), _connection);
        var r5 = await _processor.Process(Resp("GET", "user"), _connection);
        Decode(r5).Should().Be("$4\r\nlena\r\n");
    }

    // ─── List workflow ──────────────────────────────────────

    [Fact]
    public async Task ListPushPopRange_Workflow()
    {
        // RPUSH builds list: [a, b, c]
        var r1 = await _processor.Process(Resp("RPUSH", "mylist", "a"), _connection);
        Decode(r1).Should().Be(":1\r\n");
        await _processor.Process(Resp("RPUSH", "mylist", "b"), _connection);
        await _processor.Process(Resp("RPUSH", "mylist", "c"), _connection);

        // LLEN
        var r2 = await _processor.Process(Resp("LLEN", "mylist"), _connection);
        Decode(r2).Should().Be(":3\r\n");

        // LRANGE 0 -1 → all elements
        var r3 = await _processor.Process(Resp("LRANGE", "mylist", "0", "-1"), _connection);
        var decoded = Decode(r3);
        decoded.Should().Contain("a").And.Contain("b").And.Contain("c");

        // TYPE
        var r4 = await _processor.Process(Resp("TYPE", "mylist"), _connection);
        Decode(r4).Should().Be("+list\r\n");

        // LPOP removes from head
        var r5 = await _processor.Process(Resp("LPOP", "mylist"), _connection);
        Decode(r5).Should().Be("$1\r\na\r\n");

        // LLEN after pop
        var r6 = await _processor.Process(Resp("LLEN", "mylist"), _connection);
        Decode(r6).Should().Be(":2\r\n");
    }

    [Fact]
    public async Task LPush_PrependOrder_Workflow()
    {
        // LPUSH inserts at head: each new element becomes the first
        await _processor.Process(Resp("LPUSH", "stack", "a"), _connection);
        await _processor.Process(Resp("LPUSH", "stack", "b"), _connection);
        await _processor.Process(Resp("LPUSH", "stack", "c"), _connection);

        // LRANGE should show: c, b, a (LIFO)
        var result = await _processor.Process(Resp("LRANGE", "stack", "0", "-1"), _connection);
        var decoded = Decode(result);

        // Verify order: c appears before b, b before a
        int posC = decoded.IndexOf("c");
        int posB = decoded.IndexOf("b");
        int posA = decoded.IndexOf("a");
        posC.Should().BeLessThan(posB);
        posB.Should().BeLessThan(posA);
    }

    // ─── Stream workflow ────────────────────────────────────

    [Fact]
    public async Task XAdd_SequentialEntries_Workflow()
    {
        var r1 = await _processor.Process(
            Resp("XADD", "events", "1-0", "action", "click"), _connection);
        Decode(r1).Should().Be("$3\r\n1-0\r\n");

        var r2 = await _processor.Process(
            Resp("XADD", "events", "1-1", "action", "scroll"), _connection);
        Decode(r2).Should().Be("$3\r\n1-1\r\n");

        var r3 = await _processor.Process(
            Resp("XADD", "events", "2-0", "action", "hover"), _connection);
        Decode(r3).Should().Be("$3\r\n2-0\r\n");

        // TYPE
        var r4 = await _processor.Process(Resp("TYPE", "events"), _connection);
        Decode(r4).Should().Be("+stream\r\n");
    }

    [Fact]
    public async Task XAdd_OutOfOrder_ReturnsError()
    {
        await _processor.Process(
            Resp("XADD", "s", "5-0", "k", "v"), _connection);

        var result = await _processor.Process(
            Resp("XADD", "s", "3-0", "k", "v"), _connection);

        Decode(result).Should().Contain("equal or smaller");
    }

    // ─── Mixed types in KEYS ────────────────────────────────

    [Fact]
    public async Task Keys_MixedTypes_ReturnsAll()
    {
        await _processor.Process(Resp("SET", "str_key", "val"), _connection);
        await _processor.Process(Resp("RPUSH", "list_key", "item"), _connection);
        await _processor.Process(Resp("XADD", "stream_key", "1-0", "k", "v"), _connection);

        var result = await _processor.Process(Resp("KEYS", "*"), _connection);
        var decoded = Decode(result);

        decoded.Should().Contain("str_key");
        decoded.Should().Contain("list_key");
        decoded.Should().Contain("stream_key");
    }

    // ─── Config ─────────────────────────────────────────────

    [Fact]
    public async Task Config_GetDir_ReturnsValue()
    {
        var result = await _processor.Process(Resp("CONFIG", "GET", "dir"), _connection);
        Decode(result).Should().Contain("dir");
    }

    // ─── Error handling ─────────────────────────────────────

    [Fact]
    public async Task UnknownCommand_ReturnsError()
    {
        var result = await _processor.Process(Resp("NONEXISTENT"), _connection);
        Decode(result).Should().Contain("Unknown command");
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNil()
    {
        var result = await _processor.Process(Resp("GET", "nope"), _connection);
        Decode(result).Should().Be("$-1\r\n");
    }

    [Fact]
    public async Task Type_MissingKey_ReturnsNone()
    {
        var result = await _processor.Process(Resp("TYPE", "nope"), _connection);
        Decode(result).Should().Be("+none\r\n");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}