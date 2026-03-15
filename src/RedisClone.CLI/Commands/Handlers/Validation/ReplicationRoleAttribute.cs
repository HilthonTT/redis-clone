using RedisClone.CLI.Options;

namespace RedisClone.CLI.Commands.Handlers.Validation;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ReplicationRoleAttribute(ReplicationRole role) : Attribute
{
    public ReplicationRole Role { get; } = role;
}
