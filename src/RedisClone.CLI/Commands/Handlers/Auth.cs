using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Security.Cryptography;
using System.Text;

namespace RedisClone.CLI.Commands.Handlers;

// AUTH password
// AUTH username password  (Redis 6+ ACL style — username is accepted but ignored for now)
[Argument(min: 1, max: 2)]
internal sealed class Auth(AppSettings settings) : BaseCommandHandler(settings)
{
    public override bool SupportsReplication => false;

    public override CommandType CommandType => CommandType.Auth;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        if (string.IsNullOrWhiteSpace(Settings.Security.RequirePass))
        {
            connection.Authenticate();
            return RedisValue.Ok;
        }

        // AUTH password  (1 arg)
        // AUTH username password  (2 args)
        string? password = command.Arguments.Length switch
        {
            1 => command.Arguments[0],
            2 => command.Arguments[1],    // ignore username for now
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(password))
        {
            return RedisValue.ToError("ERR wrong number of arguments for 'auth' command");
        }

        if (!CryptographicEquals(password, Settings.Security.RequirePass))
        {
            return RedisValue.ToError("WRONGPASS invalid username-password pair or user is disabled");
        }

        connection.Authenticate();
        return RedisValue.Ok;
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
