namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute
{
    public string? Name { get; }

    public CommandAttribute()
    {
    }

    public CommandAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }
}