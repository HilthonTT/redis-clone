using Microsoft.Extensions.DependencyInjection;

namespace RedisClone.Client.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="RedisClient"/> in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// Registers a singleton <see cref="RedisClient"/> configured via the options delegate.
    ///
    /// <code>
    /// // Program.cs (Blazor Server or ASP.NET)
    /// builder.Services.AddRedisClient(options =>
    /// {
    ///     options.Host = "localhost";
    ///     options.Port = 6379;
    ///     options.PoolSize = 20;
    /// });
    ///
    /// // Or with a connection string
    /// builder.Services.AddRedisClient(options =>
    /// {
    ///     options.ConnectionString = "redis-server:6379";
    /// });
    /// </code>
    /// </summary>
    public static IServiceCollection AddRedisClient(
        this IServiceCollection services,
        Action<RedisClientOptions> configure)
    {
        var options = new RedisClientOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(sp => new RedisClient(sp.GetRequiredService<RedisClientOptions>()));

        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="RedisClient"/> with default options (localhost:6379).
    /// </summary>
    public static IServiceCollection AddRedisClient(this IServiceCollection services)
    {
        return services.AddRedisClient(_ => { });
    }

    /// <summary>
    /// Registers a singleton <see cref="RedisClient"/> from a connection string.
    ///
    /// <code>
    /// builder.Services.AddRedisClient("redis-server:6379");
    /// </code>
    /// </summary>
    public static IServiceCollection AddRedisClient(
        this IServiceCollection services, 
        string connectionString)
    {
        return services.AddRedisClient(o => o.ConnectionString = connectionString);
    }
}
