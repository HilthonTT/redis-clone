using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;
using RedisClone.CLI.Server;
using RedisClone.CLI.Server.Interfaces;
using RedisClone.CLI.Storage;

var settingsProvider = new SettingsProvider();
await settingsProvider.LoadSettingsAsync();
AppSettings appSettings = settingsProvider.GetSettings();

var serviceBuilder = new ServiceCollection()
    .AddSingleton<ISettingsProvider>(settingsProvider)
    .AddSingleton(appSettings)
    .AddSingleton<CommandProcessor>()
    .AddTransient<ServerInitializer>()
    .AddTransient<IWorker, TcpConnectionWorker>()
    .AddSingleton<IServer, Server>();

serviceBuilder
    .AddSingleton<KvpStorage>()
    .AddSingleton<ListStorage>()
    .AddSingleton<StreamStorage>()
    .AddSingleton<Storage>();

serviceBuilder
    .AddTransient<ICommandHandler, Get>()
    .AddTransient<ICommandHandler, Set>()
    .AddTransient<ICommandHandler, Echo>()
    .AddTransient<ICommandHandler, Ping>()
    .AddTransient<ICommandHandler, LLen>()
    .AddTransient<ICommandHandler, LLPop>()
    .AddTransient<ICommandHandler, LPush>()
    .AddTransient<ICommandHandler, LRange>();

using var serviceProvider = serviceBuilder.BuildServiceProvider();

var initializer = serviceProvider.GetRequiredService<ServerInitializer>();
await initializer.InitializeAsync(args);

var server = serviceProvider.GetRequiredService<IServer>();
await server.StartAndListenAsync();
