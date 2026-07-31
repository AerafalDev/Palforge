using Palforge.Commands.Modules;
using Palforge.Sdk.Script.Pal;
using Palforge.Unreal;

namespace Palforge.Commands.Arguments;

internal sealed class PlayerArgumentReader : ArgumentReader<PalPlayerController>
{
    private readonly UnrealApi _unreal;

    public PlayerArgumentReader(UnrealApi unreal)
    {
        _unreal = unreal;
    }

    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out PalPlayerController value, out string? errorMessage)
    {
        var name = input.ToString();

        foreach (var controller in _unreal.FindAll<PalPlayerController>())
        {
            if (controller.GetPalPlayerState() is { } state && string.Equals(state.PlayerNamePrivate, name, StringComparison.OrdinalIgnoreCase))
            {
                value = controller;
                errorMessage = null;
                return true;
            }
        }

        value = null!;
        errorMessage = $"No online player named '{name}'.";
        return false;
    }
}