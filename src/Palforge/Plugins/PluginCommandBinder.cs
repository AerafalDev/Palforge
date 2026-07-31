using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Palforge.Commands;
using Palforge.Commands.Arguments;
using Palforge.Commands.Attributes;
using Palforge.Commands.Modules;
using Palforge.Unreal;

namespace Palforge.Plugins;

internal static class PluginCommandBinder
{
    private const BindingFlags Candidates = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static int Attach(Assembly assembly, IServiceProvider services, UnrealApi unreal, PluginScope scope, CommandRegistry registry, string pluginId, ILogger log)
    {
        var readers = BuildReaders(assembly, services, unreal);
        var registered = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CommandModule).IsAssignableFrom(type))
                continue;

            var groupName = type.GetCustomAttribute<CommandGroupAttribute>()?.Name;
            var groupAliases = type.GetCustomAttribute<CommandAliasesAttribute>()?.Aliases ?? [];

            foreach (var method in type.GetMethods(Candidates))
            {
                if (!CommandBinder.IsCommand(method))
                    continue;

                var command = CommandBinder.Build(type, method, pluginId, services, groupName, groupAliases, readers);

                scope.Track(registry.Add(command));
                registered++;

                log.LogDebug("Command {Command} registered from {Type}.{Method}", command.GroupName is { } group ? $"{group} {command.Name}" : command.Name, type.Name, method.Name);
            }
        }

        return registered;
    }

    private static ArgumentReaderRegistry BuildReaders(Assembly assembly, IServiceProvider services, UnrealApi unreal)
    {
        var readers = new ArgumentReaderRegistry();

        readers.Register(new PlayerArgumentReader(unreal));

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(ArgumentReader).IsAssignableFrom(type))
                readers.Register((ArgumentReader)ActivatorUtilities.CreateInstance(services, type));
        }

        return readers;
    }
}