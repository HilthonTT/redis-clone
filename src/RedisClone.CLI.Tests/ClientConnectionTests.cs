using FluentAssertions;
using RedisClone.CLI.Subscriptions;
using RedisClone.CLI.Tests.Factories;
using System.Net.Sockets;

namespace RedisClone.CLI.Tests;

public sealed class ClientConnectionTests : IAsyncDisposable
{
    private readonly ClientConnection _connection;
    private readonly Socket _client;

    public ClientConnectionTests()
    {
        (_connection, _client) = CommandFactory.CreateConnectionPair();
    }

    [Fact]
    public void Id_ReturnsAssignedId()
    {
        _connection.Id.Should().Be(1);
    }

    [Fact]
    public void InSubscribedMode_InitiallyFalse()
    {
        _connection.InSubscribedMode.Should().BeFalse();
    }

    [Fact]
    public void IsReplicaConnection_NormalConnection_ReturnsFalse()
    {
        _connection.IsReplicaConnection.Should().BeFalse();
    }

    [Fact]
    public void IsReplicaConnection_IdMinusOne_ReturnsTrue()
    {
        var (client, server) = CommandFactory.CreateSocketPair();
        var replicaConn = new ClientConnection(-1, server);

        replicaConn.IsReplicaConnection.Should().BeTrue();

        server.Dispose();
        client.Dispose();
    }

    [Fact]
    public void LastCommandOffset_DefaultsToZero()
    {
        _connection.LastCommandOffset.Should().Be(0);
    }

    [Fact]
    public void LastCommandOffset_CanBeSet()
    {
        _connection.LastCommandOffset = 1024;
        _connection.LastCommandOffset.Should().Be(1024);
    }

    [Fact]
    public async Task EnterSubscribedMode_SetsFlag()
    {
        await _connection.EnterSubscribedModeAsync();
        _connection.InSubscribedMode.Should().BeTrue();
    }

    [Fact]
    public async Task EnterSubscribedMode_CalledTwice_IsIdempotent()
    {
        await _connection.EnterSubscribedModeAsync();
        await _connection.EnterSubscribedModeAsync();

        _connection.InSubscribedMode.Should().BeTrue();
    }

    [Fact]
    public async Task MessageWriter_AcceptsMessages()
    {
        var message = new PubSubMessage(EventType.Subscription, "ch", "msg");
        bool written = _connection.MessageWriter.TryWrite(message);

        written.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        await _connection.DisposeAsync();
        // Second call should not throw
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CompletesMessageChannel()
    {
        await _connection.DisposeAsync();

        var message = new PubSubMessage(EventType.Subscription, "ch", "msg");
        bool written = _connection.MessageWriter.TryWrite(message);

        written.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_AfterEnteringSubscribedMode_CompletesCleanly()
    {
        await _connection.EnterSubscribedModeAsync();
        _connection.InSubscribedMode.Should().BeTrue();

        // Should not hang or throw
        await _connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _client.Dispose();
    }
}
