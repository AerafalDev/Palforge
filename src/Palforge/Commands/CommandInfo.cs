using Microsoft.Extensions.DependencyInjection;
using Palforge.Commands.Arguments;
using Palforge.Commands.Attributes;
using Palforge.Commands.Modules;
using Palforge.Commands.Preconditions;

namespace Palforge.Commands;

internal sealed class CommandInfo
{
    private readonly ObjectFactory _factory;
    private readonly Action<object, object?[]> _invoke;

    public string? GroupName { get; init; }

    public IReadOnlyList<string> GroupAliases { get; init; } = [];

    public required string Name { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public string? Description { get; init; }

    public IReadOnlyList<CommandParameter> Parameters { get; init; } = [];

    public IReadOnlyList<CommandPreconditionAttribute> Preconditions { get; init; } = [];

    public required string PluginId { get; init; }

    public required IServiceProvider Services { get; init; }

    public CommandInfo(ObjectFactory factory, Action<object, object?[]> invoke)
    {
        _factory = factory;
        _invoke = invoke;
    }

    public PreconditionResult CheckPreconditions(CommandContext context)
    {
        foreach (var precondition in Preconditions)
        {
            var result = precondition.Check(context, Services);

            if (!result.IsSuccess)
                return result;
        }

        return PreconditionResult.Success;
    }

    public bool TryReadArguments(CommandContext context, ReadOnlySpan<char> arguments, out object?[] values, out string? errorMessage)
    {
        var reader = new RawArgumentReader(arguments);
        var read = new object?[Parameters.Count];

        for (var index = 0; index < Parameters.Count; index++)
        {
            var parameter = Parameters[index];

            if (parameter.IsRemainder)
            {
                var rest = reader.ReadRemainder();

                if (rest.IsEmpty && !parameter.IsOptional)
                    return Missing(parameter, out values, out errorMessage);

                parameter.Reader.TryRead(context, rest, out read[index], out _);
                continue;
            }

            if (parameter.IsParams)
            {
                if (!TryReadParams(context, ref reader, parameter, out read[index], out errorMessage))
                {
                    values = [];
                    return false;
                }

                continue;
            }

            if (!reader.TryReadToken(out var token))
            {
                if (parameter.IsOptional)
                {
                    read[index] = parameter.DefaultValue;
                    continue;
                }

                return Missing(parameter, out values, out errorMessage);
            }

            if (!parameter.Reader.TryRead(context, token, out read[index], out var reason))
            {
                values = [];
                errorMessage = $"Argument '{parameter.Name}': {reason}";
                return false;
            }
        }

        if (reader.TryReadToken(out _))
        {
            values = [];
            errorMessage = "Too many arguments.";
            return false;
        }

        values = read;
        errorMessage = null;
        return true;
    }

    public void Invoke(CommandContext context, object?[] values)
    {
        var module = _factory(Services, null);

        ((CommandModule)module).SetContext(context);

        _invoke(module, values);
    }

    private static bool TryReadParams(CommandContext context, ref RawArgumentReader reader, CommandParameter parameter, out object? value, out string? errorMessage)
    {
        var items = new List<object?>();

        while (reader.TryReadToken(out var token))
        {
            if (!parameter.Reader.TryRead(context, token, out var element, out var reason))
            {
                value = null;
                errorMessage = $"Argument '{parameter.Name}': {reason}";
                return false;
            }

            items.Add(element);
        }

        var array = Array.CreateInstance(parameter.ElementType!, items.Count);

        for (var index = 0; index < items.Count; index++)
            array.SetValue(items[index], index);

        value = array;
        errorMessage = null;
        return true;
    }

    private static bool Missing(CommandParameter parameter, out object?[] values, out string? errorMessage)
    {
        values = [];
        errorMessage = $"Missing argument '{parameter.Name}'.";
        return false;
    }
}