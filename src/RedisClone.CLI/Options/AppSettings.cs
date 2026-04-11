namespace RedisClone.CLI.Options;

public sealed class AppSettings
{
    private const string AppDirectoryName = "RedisClone";

    public static readonly AppSettings Default = new()
    {
        Runtime = new RuntimeSettings
        {
            Port = 6379,
        },
        Persistence = new PersistenceSettings
        {
            Directory = GetAppDataDirectory(),
            DbFileName = "backup.rdb",
        },
        Replication = new ReplicationSettings
        {
            Role = ReplicationRole.Master,
        },
        Security = new SecuritySettings
        {
            RequireUser = "default",
            MaxConnectionsPerIp = 50,
            MaxTotalConnections = 10_000,
            RateLimitPerSecond = 1000,
            RateLimitBurst = 200,
        },
    };

    public required RuntimeSettings Runtime { get; init; }

    public required PersistenceSettings Persistence { get; init; }

    public required ReplicationSettings Replication { get; init; }

    public required SecuritySettings Security { get; init; }

    public static string GetAppDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDirectoryName);
}
