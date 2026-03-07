using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Server;
using RedisClone.CLI.Server.Interfaces;
using RedisClone.CLI.Storage;

var settingsProvider = new SettingsProvider();
await settingsProvider.LoadSettingsAsync();
AppSettings appSettings = settingsProvider.GetSettings();

var serviceBuilder = new ServiceCollection()
    .AddSingleton(appSettings)
    .AddSingleton<CommandProcessor>()
    .AddSingleton<IServer, Server>();

serviceBuilder
    .AddSingleton<KvpStorage>()
    .AddSingleton<Storage>();

serviceBuilder
    .AddTransient<ICommandHandler, Get>()
    .AddTransient<ICommandHandler, Set>();

using var serviceProvider = serviceBuilder.BuildServiceProvider();

var server = serviceProvider.GetRequiredService<IServer>();
await server.StartAndListenAsync();
