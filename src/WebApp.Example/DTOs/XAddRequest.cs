namespace WebApp.Example.DTOs;

public sealed record XAddRequest(string StreamKey, string? Id = "*", Dictionary<string, string>? Fields = null);
