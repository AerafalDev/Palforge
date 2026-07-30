using Microsoft.Extensions.Logging;
using Palforge.Unreal;

namespace Palforge.Plugins;

internal sealed record PluginServices(ILogger Log, UnrealApi Unreal, PluginScope Scope, IServiceProvider Services);