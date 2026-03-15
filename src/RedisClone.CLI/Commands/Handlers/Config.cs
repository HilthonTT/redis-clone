using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1, max: 2)]
internal sealed class Config(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Config;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string subCommand = command.Arguments[0];

        if (!subCommand.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return RedisValue.ToError($"ERR unknown subcommand '{subCommand}'");
        }

        if (command.Arguments.Length == 1)
        {
            return RedisValue.ToError("ERR wrong number of arguments for 'config|get' command");
        }

        string configName = command.Arguments[1].ToUpperInvariant();
        return configName switch
        {
            "DIR" => RedisValue.ToBulkStringArray(["dir", Settings.Persistence.Directory]),
            "DBFILENAME" => RedisValue.ToBulkStringArray(["dbfilename", Settings.Persistence.DbFileName]),
            _ => RedisValue.NullBulkStringArray,
        };
    }
}
