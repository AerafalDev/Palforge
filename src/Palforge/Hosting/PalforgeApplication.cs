using System.Globalization;
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
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Serilog.ILogger;

namespace Palforge.Hosting;

internal static class PalforgeApplication
{
    private const string Template = "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Source}: {Message:l}{NewLine}{Exception}";

    private const int RetainedDays = 14;

    public static void Start()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.PalforgeRootDirectory)
            .AddIniFile("Configuration.cfg", false, false)
            .Build();

        var debugOptions = configuration.GetSection(DebugOptions.SectionName).Get<DebugOptions>()!;

        if (debugOptions.EnableConsole)
            ConsoleForwarder.CreateConsole();

        var loggerFactory = LoggerFactory.Create(x => x.ClearProviders().AddSerilog(ConfigureLogger(debugOptions), true));

        var commandOptions = configuration.GetSection(CommandOptions.SectionName).Get<CommandOptions>()!;

        var plugins = new PluginApi(loggerFactory.CreateLogger<PluginApi>(), loggerFactory, debugOptions, commandOptions);

        var runtimeOptions = configuration.GetSection(UnrealRuntimeOptions.SectionName).Get<UnrealRuntimeOptions>()!;

        var bootstrap = new UnrealBootstrap(loggerFactory.CreateLogger<UnrealBootstrap>(), runtimeOptions, plugins);

        var runtime = bootstrap.Start();

        GC.KeepAlive(runtime);
    }

    private static ILogger ConfigureLogger(DebugOptions debugOptions)
    {
        return Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(debugOptions.MinimumLevel)
            .Enrich.FromLogContext()
            .Enrich.With(new ShortSourceContextEnricher())
            .WriteTo.Console(outputTemplate: Template, formatProvider: CultureInfo.InvariantCulture, theme: AnsiConsoleTheme.Code, applyThemeToRedirectedOutput: true)
            .WriteTo.File(
                Path.Combine(Path.PalforgeLogsDirectory, "Palforge-.log"),
                outputTemplate: Template,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                shared: true)
            .CreateLogger();
    }
}