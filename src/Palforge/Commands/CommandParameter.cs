using Palforge.Commands.Arguments;

namespace Palforge.Commands;

internal sealed class CommandParameter
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    public required ArgumentReader Reader { get; init; }

    public Type? ElementType { get; init; }

    public bool IsRemainder { get; init; }

    public bool IsParams { get; init; }

    public bool IsOptional { get; init; }

    public object? DefaultValue { get; init; }
}