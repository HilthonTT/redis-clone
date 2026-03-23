using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class BLPopHandlerTests : IAsyncDisposable
{
    private readonly ListStorage _listStorage = new();
    private readonly PubSub _pubSub = new();
    private readonly AppSettings _settings = AppSettings.Default;
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public BLPopHandlerTests()
    {
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(CLI.Models.RedisValue v) => Encoding.UTF8.GetString(v.Value);

    [Fact]
    public async Task BLPop_ListAlreadyHasData_ReturnsImmediately()
    {
        _listStorage.AddLast("mylist", ["hello"]);

        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "1");

        var result = await handler.HandleAsync(cmd, _connection);
        var decoded = Decode(result);

        decoded.Should().Contain("mylist");
        decoded.Should().Contain("hello");
    }

    [Fact]
    public async Task BLPop_EmptyListWithTimeout_ReturnsNullAfterTimeout()
    {
        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "0.1"); // 100ms timeout

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.HandleAsync(cmd, _connection);
        sw.Stop();

        Decode(result).Should().Be("$-1\r\n");
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(50); // should have waited
    }

    [Fact]
    public async Task BLPop_DataArrivesBeforeTimeout_ReturnsValue()
    {
        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "5");

        // Push data after a short delay, then notify via pubsub
        var pushTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            _listStorage.AddLast("mylist", ["arrived"]);
            _pubSub.Publish(EventType.ListPushed, "mylist", "arrived");
        });

        var result = await handler.HandleAsync(cmd, _connection);
        await pushTask;

        var decoded = Decode(result);
        decoded.Should().Contain("mylist");
        decoded.Should().Contain("arrived");
    }

    [Fact]
    public async Task BLPop_MultipleItemsPushed_ReturnsFirstOnly()
    {
        _listStorage.AddLast("mylist", ["first", "second"]);

        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "1");

        var result = await handler.HandleAsync(cmd, _connection);
        var decoded = Decode(result);

        decoded.Should().Contain("first");
        decoded.Should().NotContain("second");
    }

    [Fact]
    public async Task BLPop_NoTimeoutArg_DefaultsToZeroTimeout()
    {
        // With only 1 arg (key, no timeout), it should use timeout=0
        // but we need data to be present or it would block forever
        _listStorage.AddLast("mylist", ["val"]);

        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist");

        var result = await handler.HandleAsync(cmd, _connection);
        Decode(result).Should().Contain("val");
    }

    [Fact]
    public async Task BLPop_TooManyArgs_ReturnsError()
    {
        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "key", "1", "extra");

        var result = await handler.HandleAsync(cmd, _connection);
        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    [Fact]
    public async Task BLPop_UnsubscribesAfterPop()
    {
        _listStorage.AddLast("mylist", ["val"]);

        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "1");

        await handler.HandleAsync(cmd, _connection);

        // After BLPop completes, publishing should deliver to 0 subscribers
        int delivered = _pubSub.Publish(EventType.ListPushed, "mylist", "orphan");
        delivered.Should().Be(0);
    }

    [Fact]
    public async Task BLPop_UnsubscribesAfterTimeout()
    {
        var handler = new BLPop(_listStorage, _settings, _pubSub);
        var cmd = CommandFactory.Create(CommandType.BLPop, "mylist", "0.1");

        await handler.HandleAsync(cmd, _connection);

        int delivered = _pubSub.Publish(EventType.ListPushed, "mylist", "orphan");
        delivered.Should().Be(0);
    }

    public async ValueTask DisposeAsync()
    {
        _pubSub.Dispose();
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
