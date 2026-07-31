using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Palforge.Commands.Options;
using Palforge.Extensions;
using Palforge.Hosting.Console;
using Palforge.Hosting.Logging;
using Palforge.Hosting.Options;
using Palforge.Plugins;
using Palforge.Unreal.Runtime;
using Serilog;

namespace Palforge.Hosting;

internal static class PalforgeApplication
{
    public static void Start()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.PalforgeRootDirectory)
            .AddIniFile("Configuration.cfg", false, false)
            .Build();

        var debugOptions = configuration.GetSection(DebugOptions.SectionName).Get<DebugOptions>()!;

        if (debugOptions.EnableConsole)
            ConsoleForwarder.CreateConsole();

        var loggerFilePath = Path.Combine(Path.PalforgeLogsDirectory, "Palforge-.log");

        var loggerFactory = LoggerFactory.Create(x => x.ClearProviders().AddSerilog(LoggingConfiguration.ConfigureLogger(debugOptions.MinimumLevel, loggerFilePath), true));

        var commandOptions = configuration.GetSection(CommandOptions.SectionName).Get<CommandOptions>()!;

        var plugins = new PluginApi(loggerFactory.CreateLogger<PluginApi>(), loggerFactory, debugOptions, commandOptions);

        var runtimeOptions = configuration.GetSection(UnrealRuntimeOptions.SectionName).Get<UnrealRuntimeOptions>()!;

        var bootstrap = new UnrealBootstrap(loggerFactory.CreateLogger<UnrealBootstrap>(), runtimeOptions, plugins);

        var runtime = bootstrap.Start();

        GC.KeepAlive(runtime);
    }
}