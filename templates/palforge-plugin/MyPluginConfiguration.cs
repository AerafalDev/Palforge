using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Palforge.Plugins;

namespace MyPlugin;

public sealed class MyPluginConfiguration : PluginConfiguration
{
    protected override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}