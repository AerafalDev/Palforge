using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Palforge.Sdk.Script.Pal;
using Palforge.Unreal;

namespace Palforge.Commands.Modules.Chat;

public sealed class ChatCommandContext : CommandContext
{
    private const int MaxMessageLength = 100;

    private readonly UnrealApi _unreal;
    private readonly string _senderName;

    public PalPlayerController Player { get; }

    public PalPlayerState? PlayerState =>
        Player.GetPalPlayerState();

    public override bool IsAdministrator =>
        Player.BAdmin;

    public override string Input { get; }

    internal ChatCommandContext(PalPlayerController player, string input, UnrealApi unreal, string senderName)
    {
        Player = player;
        Input = input;
        _unreal = unreal;
        _senderName = senderName;
    }

    public override void Reply([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_unreal.FindFirstOf<PalGameStateInGame>() is not { } gameState)
            return;

        var state = Player.GetPalPlayerState();

        if (PalChatMessage.Allocate() is not { } chat)
            return;

        using (chat)
        {
            chat.Category = EPalChatCategory.Global;
            chat.Sender = _senderName;
            chat.Message = string.Format(CultureInfo.InvariantCulture, message, args);

            if (state is not null)
                chat.ReceiverPlayerUIds.Add(state.PlayerUId);

            gameState.BroadcastChatMessage(chat);
        }
    }

    public void Broadcast([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_unreal.FindFirstOf<PalGameStateInGame>() is not { } gameState)
            return;

        if (PalChatMessage.Allocate() is not { } chat)
            return;

        using (chat)
        {
            chat.Category = EPalChatCategory.Global;
            chat.Sender = _senderName;
            chat.Message = string.Format(CultureInfo.InvariantCulture, message, args);

            gameState.BroadcastChatMessage(chat);
        }
    }

    public void Notify([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string message, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_unreal.FindFirstOf<PalGameStateInGame>() is not { } gameState)
            return;

        gameState.BroadcastServerNotice(string.Format(CultureInfo.InvariantCulture, message, args));
    }
}