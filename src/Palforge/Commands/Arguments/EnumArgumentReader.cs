using Palforge.Commands.Modules;

namespace Palforge.Commands.Arguments;

internal sealed class EnumArgumentReader<T> : ArgumentReader<T>
    where T : struct, Enum
{
    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out T value, out string? errorMessage)
    {
        if (!input.Contains(',') && Enum.TryParse(input, ignoreCase: true, out value))
        {
            errorMessage = null;
            return true;
        }

        value = default;
        errorMessage = $"'{input}' is not a valid {typeof(T).Name} — expected one of: {string.Join(", ", Enum.GetNames<T>())}.";
        return false;
    }
}