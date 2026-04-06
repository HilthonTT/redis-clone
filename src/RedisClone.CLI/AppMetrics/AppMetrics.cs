using Prometheus;

namespace RedisClone.CLI.AppMetrics;

public sealed class AppMetrics
{
    private const string Ns = "redis_clone_server";

    // Connection metrics
    public readonly Counter ConnectionsTotal;
    public readonly Gauge ActiveConnections;

    // Command metrics
    public readonly Counter CommandsTotal;
    public readonly Histogram CommandDurationSeconds;
    public readonly Counter CommandErrorsTotal;

    // Storage / Key metrics (optional but very useful)
    public readonly Gauge KeyCount;
    public readonly Gauge MemoryUsageBytes;

    // Replication metrics
    public readonly Gauge IsReplica;
    public readonly Gauge ReplicationLagSeconds;
    public readonly Counter ReplicationErrors;

    public AppMetrics()
    {
        ConnectionsTotal = Metrics.CreateCounter(
            $"{Ns}_connections_total",
            "Total number of client connections accepted");

        ActiveConnections = Metrics.CreateGauge(
            $"{Ns}_connections_active",
            "Current number of active client connections");

        CommandsTotal = Metrics.CreateCounter(
            $"{Ns}_commands_total",
            "Total number of Redis commands processed",
            new CounterConfiguration { LabelNames = ["command", "status"] });

        CommandDurationSeconds = Metrics.CreateHistogram(
            $"{Ns}_command_duration_seconds",
            "Duration of command execution in seconds",
            new HistogramConfiguration
            {
                LabelNames = ["command"],
                Buckets = Histogram.ExponentialBuckets(0.0001, 2, 12) // Fine-grained for fast Redis commands
            });

        CommandErrorsTotal = Metrics.CreateCounter(
            $"{Ns}_command_errors_total",
            "Total number of command processing errors",
            new CounterConfiguration { LabelNames = ["command", "error_type"] });

        KeyCount = Metrics.CreateGauge(
            $"{Ns}_keys_total",
            "Approximate number of keys in the database");

        MemoryUsageBytes = Metrics.CreateGauge(
            $"{Ns}_memory_usage_bytes",
            "Approximate memory usage of the in-memory store");

        IsReplica = Metrics.CreateGauge(
            $"{Ns}_is_replica",
            "1 if this instance is running as a replica, 0 if master");

        ReplicationLagSeconds = Metrics.CreateGauge(
            $"{Ns}_replication_lag_seconds",
            "Replication lag in seconds (for replicas)");

        ReplicationErrors = Metrics.CreateCounter(
            $"{Ns}_replication_errors_total",
            "Total replication errors");
    }
}