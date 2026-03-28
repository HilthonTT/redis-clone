using RedisClone.Client.DependencyInjection;
using WebApp.Example.Endpoints;
using WebApp.Example.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "RedisClone Demo API",
        Version = "v1",
        Description = "ASP.NET Core Minimal API for testing every RedisClone command via the C# client library.",
    });
});

builder.Services.AddTransient<RedisExceptionHandler>();

builder.Services.AddRedisClient(options =>
{
    // Read from config or default to localhost:6379
    string host = builder.Configuration["Redis:Host"] ?? "localhost";
    int port = int.TryParse(builder.Configuration["Redis:Port"], out int p) ? p : 6379;
    int poolSize = int.TryParse(builder.Configuration["Redis:PoolSize"], out int ps) ? ps : 10;

    options.Host = host;
    options.Port = port;
    options.PoolSize = poolSize;
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = string.Empty; // Swagger UI at /
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RedisClone Test API v1");
        options.DocumentTitle = "RedisClone Test API";
    });
}

app.UseMiddleware<RedisExceptionHandler>();

var redis = app.MapGroup("/redis")
    .WithTags("Redis Commands");

app.UseHttpsRedirection();

redis.MapStringEndpoints();
redis.MapListEndpoints();
redis.MapPubSubEndpoints();
redis.MapStreamEndpoints();
redis.MapKeyEndpoints();

await app.RunAsync();
