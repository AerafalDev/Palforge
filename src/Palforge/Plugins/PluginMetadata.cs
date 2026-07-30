using System.Reflection;
using Palforge.Plugins.Attributes;

namespace Palforge.Plugins;

internal sealed record PluginMetadata(string Id, string Name, string? Author, string Version)
{
    public static PluginMetadata For(string directoryName, Type type, Assembly assembly)
    {
        var attribute = type.GetCustomAttribute<PluginAttribute>();

        return new PluginMetadata(
            attribute?.Id ?? directoryName,
            attribute?.Name ?? directoryName,
            attribute?.Author,
            attribute?.Version ?? assembly.GetName().Version?.ToString() ?? "0.0.0");
    }

    public override string ToString()
    {
        return $"{Name} v{Version}{(Author is not null ? $" by {Author}" : string.Empty)} [{Id}]";
    }
}