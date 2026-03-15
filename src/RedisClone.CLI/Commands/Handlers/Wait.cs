using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Replication;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 2, max: 2)]
[ReplicationRole(role: ReplicationRole.Master)]
internal sealed class Wait(AppSettings settings, MasterManager masterManager) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Wait;

    public override bool SupportsReplication => false;

    public override bool LongOperation => true;

    protected override async Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection)
    {
        if (!int.TryParse(command.Arguments[0], out int expectReplicas) ||
            !int.TryParse(command.Arguments[1], out int timeoutMs))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        int upToDateReplicas = masterManager.CountReplicasWithAckOffset(connection.LastCommandOffset);
        if (upToDateReplicas >= expectReplicas)
        {
            return RedisValue.ToIntegerValue(upToDateReplicas);
        }

        await masterManager.RequestAckFromAllReplicasAsync();

        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                upToDateReplicas = masterManager.CountReplicasWithAckOffset(connection.LastCommandOffset);
                if (upToDateReplicas >= expectReplicas)
                {
                    break;
                }

                await Task.Delay(50, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached — fall through to return current count
        }

        upToDateReplicas = masterManager.CountReplicasWithAckOffset(connection.LastCommandOffset);
        return RedisValue.ToIntegerValue(upToDateReplicas);
    }
}
