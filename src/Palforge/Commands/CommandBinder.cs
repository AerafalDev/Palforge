using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Palforge.Commands.Arguments;
using Palforge.Commands.Attributes;

namespace Palforge.Commands;

internal static class CommandBinder
{
    public static bool IsCommand(MethodInfo method)
    {
        return method.IsDefined(typeof(CommandAttribute), inherit: false);
    }

    public static CommandInfo Build(Type moduleType, MethodInfo method, string pluginId, IServiceProvider services, string? groupName, IReadOnlyList<string> groupAliases, ArgumentReaderRegistry readers)
    {
        if (method.ReturnType != typeof(void))
            throw Definition(method, "a command must return void — answer the caller with Context.Reply(...)");

        if (method.IsStatic)
            throw Definition(method, "a command must be an instance method — it needs Context, which a static method cannot reach");

        var command = method.GetCustomAttribute<CommandAttribute>()!;
        var factory = ActivatorUtilities.CreateFactory(moduleType, Type.EmptyTypes);

        return new CommandInfo(factory, CommandInvoker.Compile(method))
        {
            GroupName = groupName,
            GroupAliases = groupAliases,
            Name = command.Name ?? method.Name.ToLowerInvariant(),
            Aliases = method.GetCustomAttribute<CommandAliasesAttribute>()?.Aliases ?? [],
            Description = method.GetCustomAttribute<CommandDescriptionAttribute>()?.Description,
            Parameters = Parameters(method, readers),
            Preconditions = [.. moduleType.GetCustomAttributes<CommandPreconditionAttribute>(inherit: true), .. method.GetCustomAttributes<CommandPreconditionAttribute>(inherit: true)],
            PluginId = pluginId,
            Services = services,
        };
    }

    private static CommandParameter[] Parameters(MethodInfo method, ArgumentReaderRegistry readers)
    {
        var infos = method.GetParameters();
        var result = new CommandParameter[infos.Length];
        var optionalSeen = false;

        for (var index = 0; index < infos.Length; index++)
        {
            var info = infos[index];
            var last = index == infos.Length - 1;

            if (info.IsDefined(typeof(CommandRemainderAttribute), inherit: false))
            {
                if (!last)
                    throw Definition(method, $"[CommandRemainder] parameter '{info.Name}' must be the last one");

                if (info.ParameterType != typeof(string))
                    throw Definition(method, $"[CommandRemainder] parameter '{info.Name}' must be a string");

                result[index] = new CommandParameter
                {
                    Name = info.Name!,
                    Type = typeof(string),
                    Reader = readers.Resolve(typeof(string))!,
                    IsRemainder = true,
                    IsOptional = info.HasDefaultValue,
                    DefaultValue = info.HasDefaultValue ? info.DefaultValue : null,
                };
                continue;
            }

            if (info.IsDefined(typeof(ParamArrayAttribute), inherit: false))
            {
                var element = info.ParameterType.GetElementType()!;

                result[index] = new CommandParameter
                {
                    Name = info.Name!,
                    Type = info.ParameterType,
                    ElementType = element,
                    Reader = readers.Resolve(element) ?? throw NoReader(method, info, element),
                    IsParams = true,
                };

                continue;
            }

            var optional = info.HasDefaultValue;

            if (optional)
                optionalSeen = true;
            else if (optionalSeen)
                throw Definition(method, $"required parameter '{info.Name}' follows an optional one — optional parameters must come last");

            result[index] = new CommandParameter
            {
                Name = info.Name!,
                Type = info.ParameterType,
                Reader = readers.Resolve(info.ParameterType) ?? throw NoReader(method, info, info.ParameterType),
                IsOptional = optional,
                DefaultValue = optional ? info.DefaultValue : null,
            };
        }

        return result;
    }

    private static InvalidOperationException NoReader(MethodInfo method, ParameterInfo info, Type type)
    {
        return Definition(method, $"parameter '{info.Name}' is a {type.Name}, which nothing can read from text — register a TypeReader<{type.Name}> for it");
    }

    private static InvalidOperationException Definition(MethodInfo method, string what)
    {
        return new InvalidOperationException($"{method.DeclaringType?.Name}.{method.Name}: {what}");
    }
}