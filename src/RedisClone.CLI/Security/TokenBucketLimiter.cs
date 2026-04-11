using System.Diagnostics;

namespace RedisClone.CLI.Security;

/// <summary>
/// Lock-free token-bucket rate limiter per IP.
/// Thread-safe — uses Interlocked operations only.
/// </summary>
internal sealed class TokenBucketLimiter
{
    private long _tokens; // stored as integer * 1000 for sub-second precision
    private long _lastRefillTicks;

    private readonly long _capacityUnits; // capacity * 1000
    private readonly long _refillUnitsPerTicks; // tokens added per Stopwatch tick

    public TokenBucketLimiter(int capacity, double refillPerSecond)
    {
        _capacityUnits = capacity * 1000L;
        _tokens = _capacityUnits;
        _lastRefillTicks = Stopwatch.GetTimestamp();

        // Convert refill rate from per second to per tick
        _refillUnitsPerTicks = (long)(refillPerSecond * 1000.0 / Stopwatch.Frequency);
    }

    public bool TryConsume()
    {
        Refill();

        while (true)
        {
            long current = Interlocked.Read(ref _tokens);
            if (current < 1000)
            {
                return false; // bucket empty
            }

            if (Interlocked.CompareExchange(ref _tokens, current - 1000, current) == current)
            {
                return true;
            }
            // Another thread raced — retry.
        }
    }

    private void Refill()
    {
        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref _lastRefillTicks);
        long elapsed = now - last;

        if (elapsed <= 0)
        {
            return;
        }

        long toAdd = elapsed * _refillUnitsPerTicks;
        if (toAdd <= 0)
        {
            return;
        }

        // Claim the elapsed window atomically.
        if (Interlocked.CompareExchange(ref _lastRefillTicks, now, last) != last)
        {
            return; // another thread handled refill
        }

        long current;
        do
        {
            current = Interlocked.Read(ref _tokens);
        }
        while (Interlocked.CompareExchange(
                  ref _tokens,
                  Math.Min(current + toAdd, _capacityUnits),
                  current) != current);
    }
}
