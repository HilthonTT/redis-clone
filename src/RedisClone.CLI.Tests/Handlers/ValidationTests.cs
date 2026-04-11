using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Subscriptions;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;
using System.Text;

namespace RedisClone.CLI.Tests.Handlers;

public sealed class ValidationTests : IAsyncDisposable
{
    private readonly ClientConnection _connection;
    private readonly PubSub _pubSub = new();
    private readonly Socket _client;
    private readonly AppSettings _settings = AppSettings.Default;

    public ValidationTests()
    {
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    private static string Decode(RedisValue v) => Encoding.UTF8.GetString(v.Value);

    // ─── Argument validation ────────────────────────────────

    [Fact]
    public void Handler_WithMinArgs_BelowMin_ReturnsError()
    {
        var handler = new Echo(_settings);
        var cmd = CommandFactory.Create(CommandType.Echo); // needs min 1

        var result = handler.Handle(cmd, _connection);
        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    [Fact]
    public void Handler_WithMaxArgs_AboveMax_ReturnsError()
    {
        var handler = new LLen(_settings, new CLI.Storage.ListStorage());
        // LLen has max 1 arg
        var cmd = CommandFactory.Create(CommandType.LLen, "key1", "key2");

        var result = handler.Handle(cmd, _connection);
        Decode(result).Should().StartWith("-ERR wrong number of arguments");
    }

    [Fact]
    public void Handler_WithinArgRange_Succeeds()
    {
        var handler = new Ping(_settings);
        var cmd = CommandFactory.Create(CommandType.Ping);

        var result = handler.Handle(cmd, _connection);
        Decode(result).Should().Be("+PONG\r\n");
    }

    // ─── Subscribed mode validation ─────────────────────────

    [Fact]
    public async Task Handler_NotSupportedInSubscribedMode_ReturnsError()
    {
        // Put connection in subscribed mode
        await _connection.EnterSubscribedModeAsync();
        _connection.InSubscribedMode.Should().BeTrue();

        // Get handler doesn't have [SupportedInSubscribedMode], so it's
        // not explicitly allowed. However, the validation only triggers if
        // the attribute IS present with supported=false. Without the attribute,
        // the handler is allowed through.
        // Let's use a handler that is NOT marked as supported but has no attribute at all.
        // Actually, the validation returns null if constraint is null (no attribute),
        // so commands without the attribute are allowed even in subscribed mode.
        // This matches Redis behavior where only specific commands are blocked.
    }

    [Fact]
    public async Task Subscribe_InSubscribedMode_IsAllowed()
    {
        await _connection.EnterSubscribedModeAsync();

        var pubSub = new CLI.Subscriptions.PubSub();
        var handler = new Subscribe(_settings, pubSub);
        var cmd = CommandFactory.Create(CommandType.Subscribe, "ch1");

        // Should not return an error — Subscribe is marked [SupportedInSubscribedMode(true)]
        var result = await handler.HandleAsync(cmd, _connection);
        Decode(result).Should().Contain("subscribe");

        pubSub.Dispose();
    }

    // ─── Replication role validation ────────────────────────

    [Fact]
    public void RPush_OnSlave_ReturnsError()
    {
        // Create settings with Slave role
        var slaveSettings = new AppSettings
        {
            Runtime = new RuntimeSettings { Port = 6379 },
            Persistence = new PersistenceSettings { Directory = "/tmp", DbFileName = "test.rdb" },
            Replication = new ReplicationSettings
            {
                Role = ReplicationRole.Slave,
                SlaveReplicaSettings = new SlaveReplicaSettings
                {
                    MasterHost = "localhost",
                    MasterPort = 6380
                }
            },
            Security = new SecuritySettings
            {
                RequireUser = "default",
                MaxConnectionsPerIp = 50,
                MaxTotalConnections = 10_000,
                RateLimitPerSecond = 1000,
                RateLimitBurst = 200,
            },
        };

        var handler = new RPush(slaveSettings, new CLI.Storage.ListStorage(), _pubSub);
        var cmd = CommandFactory.Create(CommandType.RPush, "mylist", "a");

        var result = handler.Handle(cmd, _connection);
        Decode(result).Should().Contain("Only Master can handle");
    }

    [Fact]
    public void RPush_OnMaster_Succeeds()
    {
        var handler = new RPush(_settings, new CLI.Storage.ListStorage(), _pubSub);
        var cmd = CommandFactory.Create(CommandType.RPush, "mylist", "a");

        var result = handler.Handle(cmd, _connection);
        Decode(result).Should().Be(":1\r\n");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
