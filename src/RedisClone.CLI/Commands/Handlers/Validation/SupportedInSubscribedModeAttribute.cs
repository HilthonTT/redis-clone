namespace RedisClone.CLI.Commands.Handlers.Validation;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class SupportedInSubscribedModeAttribute(bool supported) : Attribute
{
    public bool IsSupported { get; } = supported;
}
