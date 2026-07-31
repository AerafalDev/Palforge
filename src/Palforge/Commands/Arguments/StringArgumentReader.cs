using Palforge.Commands.Modules;

namespace Palforge.Commands.Arguments;

internal sealed class StringArgumentReader : ArgumentReader<string>
{
    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out string value, out string? errorMessage)
    {
        value = input.ToString();
        errorMessage = null;

        return true;
    }
}