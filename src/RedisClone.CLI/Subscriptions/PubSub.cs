using System.Threading.Channels;

namespace RedisClone.CLI.Subscriptions;

internal sealed class PubSub : IDisposable
{
    // topic -> (subscriberId → pipe)
    private readonly Dictionary<string, Dictionary<int, ChannelWriter<PubSubMessage>>> _subscriptions = [];

    // subscriberId -> tracked topic keys (for count and cleanup)
    private readonly Dictionary<int, Subscriber> _subscribers = [];

    private readonly ReaderWriterLockSlim _lock = new();

    private sealed class Subscriber(int id)
    {
        public readonly int Id = id;
        public readonly HashSet<string> TopicKeys = [];
        public int SubscriptionsCount => TopicKeys.Count;
    }

    public int Subscribe(EventType eventType, string topicKey, int subscriberId, ChannelWriter<PubSubMessage> pipe)
    {
        string topic = GetTopicName(eventType, topicKey);

        _lock.EnterWriteLock();
        try
        {
            if (!_subscribers.TryGetValue(subscriberId, out var subscriber))
            {
                subscriber = new Subscriber(subscriberId);
                _subscribers[subscriberId] = subscriber;
            }

            if (!_subscriptions.TryGetValue(topic, out var pipes))
            {
                pipes = [];
                _subscriptions[topic] = pipes;
            }

            // Idempotent — re-subscribing to the same topic is a no-op
            if (pipes.TryAdd(subscriberId, pipe))
            {
                subscriber.TopicKeys.Add(topic);
            }

            return subscriber.SubscriptionsCount;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int Unsubscribe(EventType eventType, string topicKey, int subscriberId)
    {
        string topic = GetTopicName(eventType, topicKey);

        _lock.EnterWriteLock();
        try
        {
            if (!_subscribers.TryGetValue(subscriberId, out var subscriber))
            {
                return 0;
            }

            if (_subscriptions.TryGetValue(topic, out var pipes) && pipes.Remove(subscriberId))
            {
                subscriber.TopicKeys.Remove(topic);

                // Clean up empty topic entries to avoid memory growth
                if (pipes.Count == 0)
                {
                    _subscriptions.Remove(topic);
                }
            }

            return subscriber.SubscriptionsCount;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int Publish(EventType eventType, string topicKey, string payload)
    {
        string topic = GetTopicName(eventType, topicKey);

        _lock.EnterReadLock();
        try
        {
            if (!_subscriptions.TryGetValue(topic, out var pipes) || pipes.Count == 0)
            {
                return 0;
            }

            return eventType switch
            {
                EventType.Subscription => BroadcastToAll(eventType, topicKey, payload, pipes),
                EventType.ListPushed => DeliverToOne(eventType, topicKey, payload, pipes),
                _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown event type")
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // Fan-out: every subscriber gets the message (SUBSCRIBE semantics)
    private static int BroadcastToAll(
         EventType eventType,
         string topicKey,
         string payload,
         Dictionary<int, ChannelWriter<PubSubMessage>> pipes)
    {
        var message = new PubSubMessage(eventType, topicKey, payload);
        int delivered = 0;
        foreach (var pipe in pipes.Values)
        {
            // TryWrite returns false only if the channel is completed (client disconnected)
            if (pipe.TryWrite(message))
                delivered++;
        }

        return delivered;
    }


    // Work-queue: first idle subscriber wins (BLPOP semantics)
    private static int DeliverToOne(
        EventType eventType,
        string topicKey,
        string payload,
        Dictionary<int, ChannelWriter<PubSubMessage>> pipes)
    {
        var message = new PubSubMessage(eventType, topicKey, payload);
        foreach (var pipe in pipes.Values)
        {
            if (pipe.TryWrite(message))
            {
                return 1;
            }
        }

        return 0;
    }

    private static string GetTopicName(EventType eventType, string topicKey) =>
        $"{eventType}:{topicKey}";

    public void Dispose() => _lock.Dispose();
}
