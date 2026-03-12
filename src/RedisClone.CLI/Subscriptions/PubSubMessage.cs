namespace RedisClone.CLI.Subscriptions;

internal sealed record PubSubMessage(EventType Type, string Channel, string Message);