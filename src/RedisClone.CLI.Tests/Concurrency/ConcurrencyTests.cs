using FluentAssertions;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;
using System.Threading.Channels;

namespace RedisClone.CLI.Tests.Concurrency;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly KvpStorage _kvpStorage = new();
    private readonly PubSub _pubSub = new();

    [Fact]
    public async Task KvpStorage_ConcurrentSetAndGet_NoExceptions()
    {
        const int iterations = 1000;

        var writers = Enumerable.Range(0, 4).Select(w =>
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    _kvpStorage.Set($"key-{w}-{i}", $"val-{w}-{i}");
                }
            }));

        var readers = Enumerable.Range(0, 4).Select(r =>
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    // May or may not find a key — that's fine, just no crashes
                    _kvpStorage.Get($"key-{r}-{i}");
                }
            }));

        await Task.WhenAll(writers.Concat(readers));

        // Verify at least some keys were written
        _kvpStorage.Keys.Should().NotBeEmpty();
    }

    [Fact]
    public async Task KvpStorage_ConcurrentRemove_NoExceptions()
    {
        // Pre-populate
        for (int i = 0; i < 500; i++)
        {
            _kvpStorage.Set($"del-{i}", "value");
        }

        var tasks = Enumerable.Range(0, 500).Select(i =>
            Task.Run(() => _kvpStorage.Remove($"del-{i}")));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task PubSub_ConcurrentSubscribeAndPublish_NoExceptions()
    {
        const int subscriberCount = 20;
        var channels = new List<Channel<PubSubMessage>>();

        // Subscribe concurrently
        var subscribeTasks = Enumerable.Range(0, subscriberCount).Select(i =>
            Task.Run(() =>
            {
                var ch = Channel.CreateUnbounded<PubSubMessage>();
                lock (channels) { channels.Add(ch); }
                _pubSub.Subscribe(EventType.Subscription, "stress-topic", i, ch.Writer);
            }));

        await Task.WhenAll(subscribeTasks);

        // Publish concurrently
        var publishTasks = Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => _pubSub.Publish(EventType.Subscription, "stress-topic", $"msg-{i}")));

        await Task.WhenAll(publishTasks);

        // All subscribers should have received some messages
        foreach (var ch in channels)
        {
            ch.Writer.TryComplete();
            int count = 0;
            while (ch.Reader.TryRead(out _)) count++;
            count.Should().Be(50);
        }
    }

    [Fact]
    public async Task PubSub_ConcurrentSubscribeUnsubscribe_NoExceptions()
    {
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() =>
            {
                var ch = Channel.CreateUnbounded<PubSubMessage>();
                _pubSub.Subscribe(EventType.Subscription, "churn", i, ch.Writer);
                _pubSub.Unsubscribe(EventType.Subscription, "churn", i);
            }));

        await Task.WhenAll(tasks);

        // After all unsubscribes, publish should reach nobody
        int delivered = _pubSub.Publish(EventType.Subscription, "churn", "ghost");
        delivered.Should().Be(0);
    }

    [Fact]
    public async Task ListStorage_ConcurrentPushAndPop_NoExceptions()
    {
        var listStorage = new ListStorage();
        const int iterations = 500;

        var pushers = Enumerable.Range(0, 4).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    listStorage.AddLast("shared-list", [$"item-{i}"]);
                }
            }));

        var poppers = Enumerable.Range(0, 4).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    listStorage.TryRemoveFirst("shared-list", out string? _);
                }
            }));

        // Run pushers and poppers concurrently — should not throw
        await Task.WhenAll(pushers.Concat(poppers));
    }

    /// <summary>
    /// StreamStorage.GetOrAdd is thread-safe (ConcurrentDictionary), but the
    /// underlying RedisStream uses non-concurrent collections internally.
    /// Concurrent appends to *different* stream keys should be safe; concurrent
    /// appends to the *same* key may produce ordering failures.
    /// This test verifies different-key concurrency doesn't throw.
    /// </summary>
    [Fact]
    public async Task StreamStorage_ConcurrentAppendsToDifferentKeys_NoExceptions()
    {
        var streamStorage = new StreamStorage();

        var tasks = Enumerable.Range(0, 50).Select(i =>
            Task.Run(() =>
            {
                // Each task writes to its own stream key — no contention on RedisStream internals
                streamStorage.TryAppend($"stream-{i}", "*",
                    new Dictionary<string, string> { ["k"] = "v" },
                    out _, out _);
            }));

        await Task.WhenAll(tasks);

        // All 50 distinct stream keys should exist
        for (int i = 0; i < 50; i++)
        {
            streamStorage.HasKey($"stream-{i}").Should().BeTrue();
        }
    }

    /// <summary>
    /// Sequential auto-ID appends to the same stream should produce unique,
    /// monotonically increasing IDs.
    /// </summary>
    [Fact]
    public void StreamStorage_SequentialAutoIdAppends_ProduceUniqueIds()
    {
        var streamStorage = new StreamStorage();
        var ids = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            bool ok = streamStorage.TryAppend("stream", "*",
                new Dictionary<string, string> { ["k"] = $"v{i}" },
                out var id, out _);

            if (ok && id is not null)
            {
                ids.Add(id);
            }
        }

        // All sequential appends should succeed
        ids.Should().HaveCount(50);
        // All IDs should be distinct
        ids.Distinct().Should().HaveCount(50);
    }

    public void Dispose()
    {
        _kvpStorage.Dispose();
        _pubSub.Dispose();
    }
}
