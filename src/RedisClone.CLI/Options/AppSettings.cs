namespace RedisClone.CLI.Options;

public sealed class AppSettings
{
    private const string AppDirectoryName = "RedisClone";

    public static readonly AppSettings Default = new()
    {
        Runtime = new RuntimeSettings
        {
            Port = 6379,
        }
    };

    public required RuntimeSettings Runtime { get; init; }

    public static string GetAppDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDirectoryName);
}