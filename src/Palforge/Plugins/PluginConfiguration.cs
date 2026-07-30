using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Palforge.Plugins;

public abstract class PluginConfiguration
{
    protected abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    internal void Configure(IServiceCollection services, IConfiguration configuration)
    {
        ConfigureServices(services, configuration);
    }
}