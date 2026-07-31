using System.Diagnostics.CodeAnalysis;
using Palforge.Commands.Modules;

namespace Palforge.Tests.Commands;

internal sealed class TestCommandContext : CommandContext
{
    public override bool IsAdministrator =>
        false;

    public override string Input =>
        string.Empty;

    public override void Reply([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
    }
}