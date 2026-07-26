using Serilog.Core;
using Serilog.Events;

namespace Palforge.Hosting.Logging;

internal sealed class ShortSourceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (logEvent.Properties.TryGetValue("SourceContext", out var value) && value is ScalarValue { Value: string context })
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Source", context[(context.LastIndexOf('.') + 1)..]));
    }
}