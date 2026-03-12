using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;
using RedisClone.CLI.Server;
using RedisClone.CLI.Server.Interfaces;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;

var settingsProvider = new SettingsProvider();
await settingsProvider.LoadSettingsAsync();
AppSettings appSettings = settingsProvider.GetSettings();

var serviceBuilder = new ServiceCollection()
    .AddSingleton<ISettingsProvider>(settingsProvider)
    .AddSingleton(appSettings)
    .AddSingleton<CommandProcessor>()
    .AddSingleton<PubSub>()
    .AddTransient<ServerInitializer>()
    .AddTransient<IWorker, TcpConnectionWorker>()
    .AddSingleton<IServer, Server>();

serviceBuilder
    .AddSingleton<KvpStorage>()
    .AddSingleton<ListStorage>()
    .AddSingleton<StreamStorage>()
    .AddSingleton<StorageManager>();

serviceBuilder
    .AddTransient<ICommandHandler, Get>()
    .AddTransient<ICommandHandler, Set>()
    .AddTransient<ICommandHandler, Echo>()
    .AddTransient<ICommandHandler, Ping>()
    .AddTransient<ICommandHandler, LLen>()
    .AddTransient<ICommandHandler, LLPop>()
    .AddTransient<ICommandHandler, LPush>()
    .AddTransient<ICommandHandler, LRange>()
    .AddTransient<ICommandHandler, RedisClone.CLI.Commands.Handlers.Type>()
    .AddTransient<ICommandHandler, Keys>()
    .AddTransient<ICommandHandler, Subscribe>()
    .AddTransient<ICommandHandler, Unsubscribe>()
    .AddTransient<ICommandHandler, Publish>();

using var serviceProvider = serviceBuilder.BuildServiceProvider();

var initializer = serviceProvider.GetRequiredService<ServerInitializer>();
await initializer.InitializeAsync(args);

var server = serviceProvider.GetRequiredService<IServer>();
await server.StartAndListenAsync();
