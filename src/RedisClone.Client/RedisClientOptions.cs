namespace RedisClone.Client;

public sealed class RedisClientOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6379;

    public int PoolSize { get; set; } = 10;

    public TimeSpan PoolTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional. When set, the client will send AUTH automatically after connecting.
    /// Matches Redis 6+ ACL style: AUTH username password.
    /// For servers without ACL configured, leave Username as "default".
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// ACL username. Defaults to "default" to match Redis open-auth behaviour.
    /// Only used when Password is set.
    /// </summary>
    public string Username { get; set; } = "default";

    /// <summary>
    /// Convenience setter. Accepts:
    ///   "host:port"
    ///   "username:password@host:port"
    ///   "default:password@host:port"
    /// </summary>
    public string? ConnectionString
    {
        get => Password is not null
            ? $"{Username}:{Password}@{Host}:{Port}"
            : $"{Host}:{Port}";
        set
        {
            if (value is null)
            {
                return;
            }

            // Strip optional scheme (redis://, rediss://)
            ReadOnlySpan<char> remaining = value.AsSpan();
            int schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd >= 0)
            {
                remaining = remaining[(schemeEnd + 3)..];
            }

            // Split credentials from host on the last '@'
            // (passwords may theoretically contain '@', so we search from the right)
            int atSign = remaining.LastIndexOf('@');
            if (atSign >= 0)
            {
                var credentials = remaining[..atSign];
                remaining = remaining[(atSign + 1)..];

                // credentials = "username:password" or just "password"
                int colon = credentials.IndexOf(':');
                if (colon >= 0)
                {
                    Username = credentials[..colon].ToString();
                    Password = credentials[(colon + 1)..].ToString();
                }
                else
                {
                    Password = credentials.ToString();
                }
            }

            // remaining = "host:port"
            string hostPort = remaining.ToString();
            string[] parts = hostPort.Contains(':')
                ? hostPort.Split(':', 2)
                : [hostPort, "6379"];

            Host = parts[0];
            if (int.TryParse(parts[1], out int port))
            {
                Port = port;
            }
        }
    }
}
