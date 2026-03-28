using RedisClone.Client.Exceptions;
using WebApp.Example.DTOs;

namespace WebApp.Example.Middleware;

public sealed class RedisExceptionHandler : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (RedisException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                RedisResponse<object>.Fail(ex.Message));
        }
        catch (RedisConnectionException ex)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                RedisResponse<object>.Fail($"Redis connection failed: {ex.Message}"));
        }
    }
}
