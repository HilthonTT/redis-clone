namespace RedisClone.Client.Exceptions;

/// <summary>
/// Thrown when the server sends a malformed RESP response.
/// </summary>
public sealed class ProtocolViolationException(string message) : Exception(message);
