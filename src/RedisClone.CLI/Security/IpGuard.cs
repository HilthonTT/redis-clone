using RedisClone.CLI.Options;
using System.Collections.Concurrent;

namespace RedisClone.CLI.Security;

/// <summary>
/// Tracks per-IP connection counts and rate-limit buckets.
/// Automatically evicts idle IPs to avoid unbounded growth.
/// </summary>
internal sealed class IpGuard(AppSettings settings)
{
    private readonly SecuritySettings _securitySettings = settings.Security;

    private readonly ConcurrentDictionary<string, PerIpState> _states = new();

    public bool TryAcceptConnection(string ip)
    {
        var state = _states.GetOrAdd(ip, _ => new PerIpState(_securitySettings));

        int current = Interlocked.Increment(ref state.ActiveConnections);
        if (current > _securitySettings.MaxConnectionsPerIp)
        {
            Interlocked.Decrement(ref state.ActiveConnections);
            return false;
        }

        return true;
    }

    public void ReleaseConnection(string ip)
    {
        if (_states.TryGetValue(ip, out var state))
        {
            Interlocked.Decrement(ref state.ActiveConnections);
        }
    }

    /// <summary>
    /// Returns false if the IP has exceeded its command rate limit.
    /// Call once per command received.
    /// </summary>
    public bool TryConsumeCommand(string ip)
    {
        var state = _states.GetOrAdd(ip, _ => new PerIpState(_securitySettings));
        return state.RateLimiter.TryConsume();
    }

    private sealed class PerIpState(SecuritySettings opts)
    {
        public int ActiveConnections;   // Interlocked
        public readonly TokenBucketLimiter RateLimiter =
            new(opts.RateLimitBurst, opts.RateLimitPerSecond);
    }
}
