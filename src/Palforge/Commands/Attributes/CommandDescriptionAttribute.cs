namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Parameter)]
public sealed class CommandDescriptionAttribute : Attribute
{
    public string Description { get; }

    public CommandDescriptionAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Description = description;
    }
}