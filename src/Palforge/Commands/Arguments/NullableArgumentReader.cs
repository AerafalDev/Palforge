using Palforge.Commands.Modules;

namespace Palforge.Commands.Arguments;

internal sealed class NullableArgumentReader<T> : ArgumentReader<T?>
    where T : struct
{
    private readonly ArgumentReader _inner;

    public NullableArgumentReader(ArgumentReader inner)
    {
        _inner = inner;
    }

    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out T? value, out string? errorMessage)
    {
        if (input.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            errorMessage = null;
            return true;
        }

        if (_inner.TryRead(context, input, out var read, out errorMessage))
        {
            value = (T)read!;
            return true;
        }

        value = null;
        return false;
    }
}