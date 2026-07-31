using Palforge.Commands.Modules;

namespace Palforge.Commands.Arguments;

public abstract class ArgumentReader
{
    public abstract Type TargetType { get; }

    public abstract bool TryRead(CommandContext context, ReadOnlySpan<char> input, out object? value, out string? errorMessage);
}

public abstract class ArgumentReader<T> : ArgumentReader
{
    public override Type TargetType =>
        typeof(T);

    public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out object? value, out string? errorMessage)
    {
        if (TryRead(context, input, out var read, out errorMessage))
        {
            value = read;

            return true;
        }

        value = null;

        return false;
    }

    public abstract bool TryRead(CommandContext context, ReadOnlySpan<char> input, out T value, out string? errorMessage);
}