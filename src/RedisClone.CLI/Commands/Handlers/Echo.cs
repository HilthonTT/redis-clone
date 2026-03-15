using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 1)]
internal sealed class Echo(AppSettings settings) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.Echo;

    public override bool SupportsReplication => false;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string value = command.Arguments[0];
        return RedisValue.ToSimpleString(value);
    }
}
