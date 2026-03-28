namespace RedisClone.Client.Exceptions;

public sealed class RedisConnectionException(string message) : Exception(message);
