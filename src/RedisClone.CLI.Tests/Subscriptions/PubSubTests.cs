using FluentAssertions;
using RedisClone.CLI.Subscriptions;
using System.Threading.Channels;

namespace RedisClone.CLI.Tests.Subscriptions;

public sealed class PubSubTests : IDisposable
{
    private readonly PubSub _pubSub = new();

    private static ChannelWriter<PubSubMessage> CreatePipe(out ChannelReader<PubSubMessage> reader)
    {
        var ch = Channel.CreateUnbounded<PubSubMessage>();
        reader = ch.Reader;
        return ch.Writer;
    }

    [Fact]
    public void Subscribe_ReturnsSubscriptionCount()
    {
        var writer = CreatePipe(out _);

        int count1 = _pubSub.Subscribe(EventType.Subscription, "ch1", subscriberId: 1, writer);
        int count2 = _pubSub.Subscribe(EventType.Subscription, "ch2", subscriberId: 1, writer);

        count1.Should().Be(1);
        count2.Should().Be(2);
    }

    [Fact]
    public void Subscribe_SameTopicTwice_IsIdempotent()
    {
        var writer = CreatePipe(out _);

        int count1 = _pubSub.Subscribe(EventType.Subscription, "ch1", subscriberId: 1, writer);
        int count2 = _pubSub.Subscribe(EventType.Subscription, "ch1", subscriberId: 1, writer);

        count1.Should().Be(1);
        count2.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_DecreasesCount()
    {
        var writer = CreatePipe(out _);

        _pubSub.Subscribe(EventType.Subscription, "ch1", subscriberId: 1, writer);
        _pubSub.Subscribe(EventType.Subscription, "ch2", subscriberId: 1, writer);

        int remaining = _pubSub.Unsubscribe(EventType.Subscription, "ch1", subscriberId: 1);
        remaining.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_UnknownSubscriber_ReturnsZero()
    {
        int count = _pubSub.Unsubscribe(EventType.Subscription, "ch1", subscriberId: 999);
        count.Should().Be(0);
    }

    [Fact]
    public void Unsubscribe_NonSubscribedTopic_DoesNotChangeCount()
    {
        var writer = CreatePipe(out _);
        _pubSub.Subscribe(EventType.Subscription, "ch1", subscriberId: 1, writer);

        int count = _pubSub.Unsubscribe(EventType.Subscription, "ch_other", subscriberId: 1);
        count.Should().Be(1);
    }

    [Fact]
    public void Publish_Subscription_BroadcastsToAllSubscribers()
    {
        var w1 = CreatePipe(out var r1);
        var w2 = CreatePipe(out var r2);

        _pubSub.Subscribe(EventType.Subscription, "news", subscriberId: 1, w1);
        _pubSub.Subscribe(EventType.Subscription, "news", subscriberId: 2, w2);

        int delivered = _pubSub.Publish(EventType.Subscription, "news", "hello");

        delivered.Should().Be(2);
        r1.TryRead(out var msg1).Should().BeTrue();
        r2.TryRead(out var msg2).Should().BeTrue();
        msg1!.Message.Should().Be("hello");
        msg2!.Message.Should().Be("hello");
    }

    [Fact]
    public void Publish_NoSubscribers_ReturnsZero()
    {
        int delivered = _pubSub.Publish(EventType.Subscription, "empty", "hello");
        delivered.Should().Be(0);
    }

    [Fact]
    public void Publish_ListPushed_DeliversToOneSubscriber()
    {
        var w1 = CreatePipe(out var r1);
        var w2 = CreatePipe(out var r2);

        _pubSub.Subscribe(EventType.ListPushed, "mylist", subscriberId: 1, w1);
        _pubSub.Subscribe(EventType.ListPushed, "mylist", subscriberId: 2, w2);

        int delivered = _pubSub.Publish(EventType.ListPushed, "mylist", "item");

        delivered.Should().Be(1);

        // Exactly one reader should have the message
        bool got1 = r1.TryRead(out _);
        bool got2 = r2.TryRead(out _);
        (got1 ^ got2).Should().BeTrue("exactly one subscriber should receive a ListPushed message");
    }

    [Fact]
    public void Publish_CompletedChannel_SkipsDeadSubscriber()
    {
        var w1 = CreatePipe(out var r1);
        var w2 = CreatePipe(out var r2);

        _pubSub.Subscribe(EventType.Subscription, "ch", subscriberId: 1, w1);
        _pubSub.Subscribe(EventType.Subscription, "ch", subscriberId: 2, w2);

        // Complete subscriber 1's channel to simulate disconnect
        w1.TryComplete();

        int delivered = _pubSub.Publish(EventType.Subscription, "ch", "msg");

        delivered.Should().Be(1);
        r2.TryRead(out var msg).Should().BeTrue();
        msg!.Message.Should().Be("msg");
    }

    [Fact]
    public void Publish_DifferentEventTypes_AreIsolated()
    {
        var w1 = CreatePipe(out var r1);
        var w2 = CreatePipe(out var r2);

        _pubSub.Subscribe(EventType.Subscription, "topic", subscriberId: 1, w1);
        _pubSub.Subscribe(EventType.ListPushed, "topic", subscriberId: 2, w2);

        int delivered = _pubSub.Publish(EventType.Subscription, "topic", "msg");

        delivered.Should().Be(1);
        r1.TryRead(out _).Should().BeTrue();
        r2.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Publish_MessageContainsCorrectMetadata()
    {
        var writer = CreatePipe(out var reader);
        _pubSub.Subscribe(EventType.Subscription, "events", subscriberId: 1, writer);

        _pubSub.Publish(EventType.Subscription, "events", "payload");

        reader.TryRead(out var msg).Should().BeTrue();
        msg!.Type.Should().Be(EventType.Subscription);
        msg.Channel.Should().Be("events");
        msg.Message.Should().Be("payload");
    }

    public void Dispose() => _pubSub.Dispose();
}
