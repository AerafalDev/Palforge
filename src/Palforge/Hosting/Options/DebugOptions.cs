using Serilog.Events;

namespace Palforge.Hosting.Options;

internal sealed class DebugOptions
{
    public const string SectionName = "Debug";

    public required bool EnableConsole { get; set; }

    public required bool EnableDebugger { get; set; }

    public required LogEventLevel MinimumLevel { get; set; }

    public required Dictionary<string, LogEventLevel> Overrides { get; set; }
}