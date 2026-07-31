namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandGroupAttribute : Attribute
{
    public string Name { get; }

    public CommandGroupAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }
}