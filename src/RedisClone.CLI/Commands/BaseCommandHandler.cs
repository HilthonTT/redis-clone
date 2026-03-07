using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands;

internal abstract class BaseCommandHandler(AppSettings settings) : ICommandHandler
{
    private static readonly ConcurrentDictionary<Type, ArgumentAttribute?> _argumentAttributeCache = new();

    public abstract CommandType CommandType { get; }

    protected AppSettings Settings { get; } = settings;

    public RedisValue Handle(Command command, Socket socket)
    {
        RedisValue? validationError = ValidateArguments(command);
        if (validationError is not null)
        {
            return validationError;
        }

        return HandleSpecific(command, socket);
    }

    protected abstract RedisValue HandleSpecific(Command command, Socket socket);

    private RedisValue? ValidateArguments(Command command)
    {
        ArgumentAttribute? constraint = _argumentAttributeCache.GetOrAdd(
            GetType(),
            t => (ArgumentAttribute?)Attribute.GetCustomAttribute(t, typeof(ArgumentAttribute)));

        if (constraint is null)
        {
            return null;
        }

        if (command.Arguments.Length >= constraint.Min && command.Arguments.Length <= constraint.Max)
        {
            return null;
        }

        return RedisValue.ToError(
            $"ERR wrong number of arguments for '{command.Type.ToString().ToLowerInvariant()}'");
    }
}