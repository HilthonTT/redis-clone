using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Server;

var serviceBuilder = new ServiceCollection()
    .AddSingleton<Server>();

using var serviceProvider = serviceBuilder.BuildServiceProvider();

var server = serviceProvider.GetRequiredService<Server>();
await server.StartAndListenAsync();
