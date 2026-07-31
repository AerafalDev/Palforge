using System.Diagnostics.CodeAnalysis;

namespace Palforge.Commands.Modules.Chat;

public abstract class ChatCommandModule : CommandModule
{
    protected ChatCommandContext Context { get; private set; } = null!;

    internal sealed override void SetContext(CommandContext context)
    {
        Context = (ChatCommandContext)context;
    }

    protected void Reply([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        Context.Reply(message, args);
    }

    protected void Broadcast([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        Context.Broadcast(message, args);
    }

    protected void Notify([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        Context.Notify(message, args);
    }
}