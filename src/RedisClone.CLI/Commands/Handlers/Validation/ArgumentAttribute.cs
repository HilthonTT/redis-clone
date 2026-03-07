namespace RedisClone.CLI.Commands.Handlers.Validation;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ArgumentAttribute(int min = 0, int max = int.MaxValue) : Attribute
{
    public int Min { get; } = min;
    public int Max { get; } = max;
}
