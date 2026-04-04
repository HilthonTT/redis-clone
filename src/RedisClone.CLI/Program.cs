using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Commands.Handlers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;
using RedisClone.CLI.Persistence;
using RedisClone.CLI.Replication;
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
    .AddSingleton<RdbParser>()
    .AddTransient<ServerInitializer>()
    .AddSingleton<ReplicaManager>()
    .AddSingleton<MasterManager>()
    .AddTransient<IWorker, TcpConnectionWorker>()
    .AddSingleton<IServer, Server>();

serviceBuilder
    .AddSingleton<KvpStorage>()
    .AddSingleton<ListStorage>()
    .AddSingleton<StreamStorage>()
    .AddSingleton<StorageManager>();

serviceBuilder
    // Strings
    .AddTransient<ICommandHandler, Get>()
    .AddTransient<ICommandHandler, Set>()
    .AddTransient<ICommandHandler, Incr>()
    .AddTransient<ICommandHandler, Decr>()
    .AddTransient<ICommandHandler, IncrBy>()
    .AddTransient<ICommandHandler, DecrBy>()

    // Server
    .AddTransient<ICommandHandler, Echo>()
    .AddTransient<ICommandHandler, Ping>()
    .AddTransient<ICommandHandler, Config>()

    // Keys
    .AddTransient<ICommandHandler, Keys>()
    .AddTransient<ICommandHandler, RedisClone.CLI.Commands.Handlers.Type>()
    .AddTransient<ICommandHandler, Delete>()
    .AddTransient<ICommandHandler, Exists>()
    .AddTransient<ICommandHandler, Expire>()
    .AddTransient<ICommandHandler, PExpire>()
    .AddTransient<ICommandHandler, Ttl>()

    // Lists
    .AddTransient<ICommandHandler, LLen>()
    .AddTransient<ICommandHandler, LLPop>()
    .AddTransient<ICommandHandler, LPush>()
    .AddTransient<ICommandHandler, RPush>()
    .AddTransient<ICommandHandler, RPop>()
    .AddTransient<ICommandHandler, LRange>()
    .AddTransient<ICommandHandler, BLPop>()

    // Pub/Sub
    .AddTransient<ICommandHandler, Subscribe>()
    .AddTransient<ICommandHandler, Unsubscribe>()
    .AddTransient<ICommandHandler, Publish>()

    // Replication
    .AddTransient<ICommandHandler, Wait>()

    // Streams
    .AddTransient<ICommandHandler, XAdd>()
    .AddTransient<ICommandHandler, XRange>()
    .AddTransient<ICommandHandler, XRead>()

    // Transactions
    .AddTransient<ICommandHandler, Multi>()
    .AddTransient<ICommandHandler, Exec>()
    .AddTransient<ICommandHandler, Discard>();

using var serviceProvider = serviceBuilder.BuildServiceProvider();

var initializer = serviceProvider.GetRequiredService<ServerInitializer>();
await initializer.InitializeAsync(args);

var masterManager = serviceProvider.GetRequiredService<MasterManager>();
masterManager.StartReplication();

var replicationManager = serviceProvider.GetRequiredService<ReplicaManager>();
await replicationManager.ConnectToMasterAsync();

var server = serviceProvider.GetRequiredService<IServer>();
await server.StartAndListenAsync();
