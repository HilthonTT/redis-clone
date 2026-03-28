namespace WebApp.Example.DTOs;

public sealed record LRangeRequest(string Key, int Start = 0, int End = -1);