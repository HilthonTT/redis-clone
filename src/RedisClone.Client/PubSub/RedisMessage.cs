namespace RedisClone.Client.PubSub;

public sealed record RedisMessage(string Channel, string Message);
