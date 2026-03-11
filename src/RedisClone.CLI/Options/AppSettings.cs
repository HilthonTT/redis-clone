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
        }
    };

    public required RuntimeSettings Runtime { get; init; }

    public required PersistenceSettings Persistence { get; init; }

    public static string GetAppDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDirectoryName);
}
