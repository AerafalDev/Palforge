using System.Diagnostics.CodeAnalysis;

namespace Palforge.Commands.Modules;

public abstract class CommandContext
{
    public abstract bool IsAdministrator { get; }

    public abstract string Input { get; }

    public abstract void Reply([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args);
}