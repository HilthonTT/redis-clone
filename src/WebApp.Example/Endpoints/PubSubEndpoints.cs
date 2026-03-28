using RedisClone.Client;
using WebApp.Example.DTOs;

namespace WebApp.Example.Endpoints;

public static class PubSubEndpoints
{
    public static RouteGroupBuilder MapPubSubEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/publish", async (PublishRequest request, RedisClient redis) =>
        {
            long reached = await redis.PublishAsync(request.Channel, request.Message);
            return Results.Ok(RedisResponse<long>.Ok(reached));
        })
        .WithName("Publish")
        .WithSummary("PUBLISH channel message — send message to subscribers");

        // SSE endpoint: subscribe to a channel and stream messages as Server-Sent Events.
        // Test with: curl -N http://localhost:5000/redis/subscribe/news
        // Then in another terminal: curl -X POST http://localhost:5000/redis/publish -H "Content-Type: application/json" -d '{"channel":"news","message":"hello"}'
        group.MapGet("/subscribe/{channel}", async (
            string channel, RedisClient redis, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            await using var subscriber = await redis.SubscribeAsync(channel);

            await foreach (var msg in subscriber.Messages(ct))
            {
                string ssePayload = $"event: message\ndata: {{\"channel\":\"{msg.Channel}\",\"message\":\"{msg.Message}\"}}\n\n";
                await ctx.Response.WriteAsync(ssePayload, ct);
                await ctx.Response.Body.FlushAsync(ct);
            }
        })
        .WithName("Subscribe")
        .WithSummary("SUBSCRIBE channel — SSE stream of pub/sub messages (use curl -N to test)");

        return group;
    }
}
