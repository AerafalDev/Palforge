using System.Globalization;
using Palforge.Commands.Modules;

namespace Palforge.Commands.Arguments;

internal sealed class ParsableArgumentReader<T> : ArgumentReader<T>
    where T : ISpanParsable<T>
{
    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out T value, out string? errorMessage)
    {
        if (T.TryParse(input, CultureInfo.InvariantCulture, out value!))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = $"'{input}' is not a valid {typeof(T).Name}.";
        return false;
    }
}