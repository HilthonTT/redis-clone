using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Collections.Concurrent;

namespace RedisClone.CLI.Commands;

internal abstract class BaseCommandHandler(AppSettings settings) : ICommandHandler
{
    private static readonly ConcurrentDictionary<Type, ArgumentAttribute?> _argumentAttributeCache = new();

    public abstract CommandType CommandType { get; }

    public virtual bool LongOperation => false;

    protected AppSettings Settings { get; } = settings;

    public RedisValue Handle(Command command, ClientConnection connection)
    {
        RedisValue? validationError = ValidateArguments(command);
        if (validationError is not null)
        {
            return validationError;
        }

        return HandleSpecific(command, connection);
    }

    public async Task<RedisValue> HandleAsync(Command command, ClientConnection connection)
    {
        RedisValue? validationError = ValidateArguments(command);
        if (validationError is not null)
        {
            return validationError;
        }

        return await HandleSpecificAsync(command, connection);
    }

    protected virtual RedisValue HandleSpecific(Command command, ClientConnection connection) => 
        throw new NotImplementedException();

    protected virtual Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection) => 
        throw new NotImplementedException();

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