using System.Globalization;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Palforge.Hosting.Logging;

internal static class LoggingConfiguration
{
    private const string Template = "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Source}: {Message:l}{NewLine}{Exception}";

    private const int RetainedDays = 14;

    public static ILogger ConfigureLogger(LogEventLevel minimumLevel, string logFilePath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.With(new ShortSourceContextEnricher())
            .WriteTo.Console(outputTemplate: Template, formatProvider: CultureInfo.InvariantCulture, theme: AnsiConsoleTheme.Code, applyThemeToRedirectedOutput: true)
            .WriteTo.File(
                logFilePath,
                outputTemplate: Template,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                shared: true)
            .CreateLogger();
    }
}