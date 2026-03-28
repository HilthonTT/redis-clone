using RedisClone.Client;
using WebApp.Example.DTOs;

namespace WebApp.Example.Endpoints;

public static class KeyEndpoints
{
    public static RouteGroupBuilder MapKeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/type/{key}", async (string key, RedisClient redis) =>
        {
            string? type = await redis.TypeAsync(key);
            return Results.Ok(RedisResponse<string>.Ok(type ?? "none"));
        })
        .WithName("Type")
        .WithSummary("TYPE key — returns the data type stored at key");

        group.MapGet("/keys", async (string pattern, RedisClient redis) =>
        {
            var keys = await redis.KeysAsync(pattern);
            return Results.Ok(RedisResponse<List<string?>>.Ok(keys));
        })
        .WithName("Keys")
        .WithSummary("KEYS pattern — return all matching keys (only * supported)");

        group.MapGet("/config/{parameter}", async (string parameter, RedisClient redis) =>
        {
            var values = await redis.ConfigGetAsync(parameter);
            return Results.Ok(RedisResponse<List<string?>>.Ok(values));
        })
        .WithName("ConfigGet")
        .WithSummary("CONFIG GET parameter — return server configuration value");

        return group;
    }
}
