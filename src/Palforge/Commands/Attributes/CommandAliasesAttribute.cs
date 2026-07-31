namespace Palforge.Commands.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class CommandAliasesAttribute : Attribute
{
    public string[] Aliases { get; }

    public CommandAliasesAttribute(params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        Aliases = aliases;
    }
}