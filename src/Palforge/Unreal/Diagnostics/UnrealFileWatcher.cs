using Microsoft.Extensions.Logging;
using Palforge.Unreal.Runtime;

namespace Palforge.Unreal.Diagnostics;

internal sealed class UnrealFileWatcher
{
    private const int IntervalTicks = 120;

    private readonly UnrealRuntime _runtime;
    private readonly Action _run;
    private readonly ILogger _log;

    private int _ticks;

    public string TriggerPath { get; }

    public UnrealFileWatcher(UnrealRuntime runtime, string triggerPath, Action run, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrEmpty(triggerPath);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(log);

        _runtime = runtime;
        _run = run;
        _log = log;
        TriggerPath = triggerPath;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(triggerPath) ?? ".");
        }
        catch (Exception exception)
        {
            _log.LogWarning(exception, "Could not create the directory for the trigger '{Trigger}'", triggerPath);
        }
    }

    public void Tick()
    {
        if (++_ticks < IntervalTicks)
            return;

        _ticks = 0;

        if (!_runtime.IsReady || !File.Exists(TriggerPath))
            return;

        try
        {
            File.Delete(TriggerPath);
        }
        catch (Exception exception)
        {
            _log.LogWarning(exception, "Could not remove the trigger '{Trigger}' — skipping this run rather than repeating it every check", TriggerPath);

            return;
        }

        try
        {
            _run();
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "The work triggered by '{Trigger}' threw", TriggerPath);
        }
    }
}