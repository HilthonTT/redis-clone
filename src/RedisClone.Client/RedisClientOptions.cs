namespace RedisClone.Client;

public sealed class RedisClientOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6379;

    public int PoolSize { get; set; } = 10;

    public TimeSpan PoolTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public string? ConnectionString
    {
        get => $"{Host}:{Port}";
        set
        {
            if (value is null)
            {
                return;
            }

            string[] parts = value.Contains(':')
                ? value.Split(':', 2)
                : [value, "6379"];

            Host = parts[0];
            if (int.TryParse(parts[1], out int port))
            {
                Port = port;
            }
        }
    }
}
