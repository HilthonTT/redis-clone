namespace RedisClone.CLI.Options;

public sealed class SecuritySettings
{
    /// <summary>
    /// Gets or sets the password required to authenticate with the server.
    /// </summary>
    /// <remarks>If set to <see langword="null"/> or an empty string, password authentication is not required.
    /// Changing this property affects subsequent authentication attempts.</remarks>
    public string? RequirePass { get; set; }

    /// <summary>
    /// Gets or sets the user requirement setting for the operation.
    /// </summary>
    public required string RequireUser { get; set; }

    /// <summary>Maximum simultaneous connections from a single IP.</summary>
    public required int MaxConnectionsPerIp { get; set; }

    /// <summary>Maximum total connections the server will accept.</summary>
    public required int MaxTotalConnections { get; set; }

    /// <summary>Commands/second sustained rate per IP.</summary>
    public required double RateLimitPerSecond { get; set; }

    /// <summary>Burst allowance (bucket capacity).</summary>
    public required int RateLimitBurst { get; set; }
}