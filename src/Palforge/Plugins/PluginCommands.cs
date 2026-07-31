using Palforge.Commands.Attributes;
using Palforge.Commands.Modules.Chat;

namespace Palforge.Plugins;

[CommandGroup("plugin")]
[CommandPermissions("palforge.admin")]
internal sealed class PluginCommands : ChatCommandModule
{
    private readonly PluginApi _plugins;

    public PluginCommands(PluginApi plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        _plugins = plugins;
    }

    [Command("list"), CommandDescription("List the loaded plugins")]
    public void List()
    {
        var plugins = _plugins.List();

        if (plugins.Count is 0)
        {
            Reply("No plugins loaded.");
            return;
        }

        Reply($"Plugins ({plugins.Count}):");

        foreach (var plugin in plugins)
            Reply($"- {plugin.Name} v{plugin.Version} [{plugin.Id}]");
    }

    [Command("load"), CommandDescription("Load a plugin by its folder name")]
    public void Load(string name)
    {
        Reply(_plugins.Load(name)
            ? $"Loaded '{name}'."
            : $"Could not load '{name}' — already loaded, not found, or it failed to start (see the log).");
    }

    [Command("unload"), CommandDescription("Unload a plugin by id or name")]
    public void Unload(string plugin)
    {
        Reply(_plugins.Unload(plugin) ? $"Unloaded '{plugin}'." : $"No plugin '{plugin}'.");
    }

    [Command("reload"), CommandDescription("Reload a plugin by id or name")]
    public void Reload(string plugin)
    {
        Reply(_plugins.Reload(plugin) ? $"Reloaded '{plugin}'." : $"No plugin '{plugin}'.");
    }
}