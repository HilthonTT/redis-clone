using Microsoft.Extensions.DependencyInjection;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

internal sealed class Exec(
    AppSettings settings,
    IServiceProvider serviceProvider) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Exec;

    public override bool SupportsReplication => false;

    public override bool LongOperation => true;

    protected override async Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection)
    {
        if (!connection.InTransactionMode)
        {
            return RedisValue.ToError("ERR EXEC without MULTI");
        }

        var processor = serviceProvider.GetRequiredService<CommandProcessor>();
        return await processor.ExecuteTransaction(connection);
    }
}
