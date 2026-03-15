using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using System.Collections.Concurrent;

namespace RedisClone.CLI.Commands;

internal abstract class BaseCommandHandler(AppSettings settings) : ICommandHandler
{
    private static readonly ConcurrentDictionary<(Type Handler, Type Attr), Attribute?> _attributeCache = new();

    private T? GetCachedAttribute<T>() where T : Attribute =>
        (T?)_attributeCache.GetOrAdd(
            (GetType(), typeof(T)),
            key => Attribute.GetCustomAttribute(key.Handler, key.Attr));

    public abstract bool SupportsReplication { get; }

    public abstract CommandType CommandType { get; }

    public virtual bool LongOperation => false;

    protected AppSettings Settings { get; } = settings;

    public RedisValue Handle(Command command, ClientConnection connection)
    {
        RedisValue? error = Validate(command, connection);
        if (error is not null)
        {
            return error;
        }
        return HandleSpecific(command, connection);
    }

    public async Task<RedisValue> HandleAsync(Command command, ClientConnection connection)
    {
        RedisValue? error = Validate(command, connection);
        if (error is not null)
        {
            return error;
        }
        return await HandleSpecificAsync(command, connection);
    }

    protected virtual RedisValue HandleSpecific(Command command, ClientConnection connection) => 
        throw new NotImplementedException();

    protected virtual Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection) => 
        throw new NotImplementedException();

    private RedisValue? Validate(Command command, ClientConnection connection) =>
        ValidateArguments(command)
        ?? ValidateRole(command, connection)
        ?? ValidateSubscribedMode(command, connection);

    private RedisValue? ValidateArguments(Command command)
    {
        ArgumentAttribute? constraint = GetCachedAttribute<ArgumentAttribute>();
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

    private RedisValue? ValidateRole(Command command, ClientConnection connection)
    {
        ReplicationRoleAttribute? constraint = GetCachedAttribute<ReplicationRoleAttribute>();

        if (constraint is null)
        {
            return null;
        }

        if (SupportsReplication && connection.IsReplicaConnection || Settings.Replication.Role == constraint.Role)
        {
            return null;
        }

        return RedisValue.ToError($"Only {constraint.Role} can handle {command.Type} command");
    }

    private RedisValue? ValidateSubscribedMode(Command command, ClientConnection connection)
    {
        SupportedInSubscribedModeAttribute? constraint = GetCachedAttribute<SupportedInSubscribedModeAttribute>();

        if (constraint is null)
        {
            return null;
        }

        if (!connection.InSubscribedMode)
        {
            return null;
        }

        if (constraint.IsSupported)
        {
            return null;
        }

        return RedisValue.ToError(
            $"ERR Can't execute '{command.Type.ToString().ToLowerInvariant()}': only (P|S)SUBSCRIBE / (P|S)UNSUBSCRIBE / PING / QUIT / RESET are allowed in this context");
    }
}
