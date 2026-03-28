namespace RedisClone.Client.Exceptions;

public sealed class RedisException(string message) : Exception(message);
