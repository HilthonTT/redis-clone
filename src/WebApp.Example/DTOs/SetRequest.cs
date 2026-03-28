namespace WebApp.Example.DTOs;

public sealed record SetRequest(string Key, string Value, long? ExpiryMs = null);