using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Palforge.Plugins;

internal sealed record PluginLoaded(
    string Name,
    PluginMetadata Metadata,
    Plugin Instance,
    PluginScope Scope,
    ServiceProvider Services,
    IConfiguration Configuration,
    PluginLoadContext Context,
    string Directory);