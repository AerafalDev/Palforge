using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Palforge.Extensions;
using Palforge.Images;
using Palforge.Layout;
using Palforge.Memory;
using Palforge.Plugins;
using Palforge.Unreal.Diagnostics;
using Palforge.Unreal.Hooks;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Sdk;
using Palforge.Unreal.Stage;
using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Runtime;

internal sealed class UnrealBootstrap
{
    private const int ScratchSize = 8192;

    private readonly ILogger<UnrealBootstrap> _log;
    private readonly UnrealRuntimeOptions _runtimeOptions;
    private readonly PluginApi _plugins;

    public UnrealBootstrap(ILogger<UnrealBootstrap> log, UnrealRuntimeOptions runtimeOptions, PluginApi plugins)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(plugins);

        _log = log;
        _runtimeOptions = runtimeOptions;
        _plugins = plugins;
    }

    public UnrealRuntime Start()
    {
        var scratch = Marshal.AllocHGlobal(ScratchSize);
        var memory = new DirectMemory(RegionMap.FromCurrentProcess());
        var probed = new ProbedMemory();

        if (!ModuleImage.TryParse(memory, MainModule.BaseAddress, out var image))
            return Disabled("the main module is not a PE image the runtime can parse");

        var anchors = AnchorSet.Resolve(memory, image);

        Report(anchors);

        if (!anchors.DerivationReady)
            return Disabled("the anchors required for derivation did not resolve");

        var construct = anchors.FNameConstructor.TryGetValue(out var constructor) ? constructor : 0;
        var natives = new NativeFNameNatives(construct, anchors.FNameToString.ValueOrThrow());
        var names = new NameResolver(probed, natives, scratch, ScratchSize);

        if (!anchors.GameEngineTick.IsResolved)
            return Disabled("UGameEngine::Tick did not resolve, so derivation cannot be deferred to the game thread");

        var objectArray = anchors.GUObjectArray.ValueOrThrow();
        var table = new LayoutBuilder().AddTable(UnrealVersionTable.Version, UnrealVersionTable.Offsets).Build();

        var runtime = UnrealRuntime.Arming(anchors, _log);

        var watchers = new List<UnrealFileWatcher>();

        if (SdkRequest() is var (watchOutput, watchRoots))
            watchers.Add(new UnrealFileWatcher(runtime, Path.Combine(watchOutput, ".regenerate"), () => SdkGenerator.Generate(runtime, watchOutput, watchRoots, _log), _log));

        watchers.Add(new UnrealFileWatcher(runtime, Path.Combine(Path.PalforgeDumpsDirectory, ".dump"), () => UnrealDumper.Dump(runtime, Path.PalforgeDumpsDirectory, _log), _log));

        var pump = new GameThreadPump(objectArray, table, names, anchors.GameEngineTick.ValueOrThrow(), _log, () => Derive(objectArray, names, runtime))
        {
            Heartbeat = Heartbeat
        };

        GameThread.UsePost(pump.Post);
        GameThread.UseScheduler(runtime.Scheduler.AfterFrames);

        pump.StartInstalling();

        _log.LogInformation("Palforge armed — derivation deferred to the game thread (waiting for the engine to warm up)");

        foreach (var armed in watchers)
            _log.LogInformation("On-demand dump armed — create '{Trigger}' in-game to run it against the live graph", armed.TriggerPath);

        return runtime;

        void Heartbeat()
        {
            runtime.OnGameThreadTick();

            foreach (var watcher in watchers)
                watcher.Tick();
        }
    }

    private (string Output, string[] Roots)? SdkRequest()
    {
        if (!_runtimeOptions.GenerateSdk)
            return null;

        var output = !string.IsNullOrEmpty(_runtimeOptions.SdkOutput)
            ? _runtimeOptions.SdkOutput
            : Path.Combine(Path.PalforgeRootDirectory, "Sdk");

        return (output, ["*"]);
    }

    private void Derive(nint objectArray, INameResolver names, UnrealRuntime runtime)
    {
        var memory = new DirectMemory(RegionMap.FromCurrentProcess());

        var layout = LayoutDeriver.Derive(memory, objectArray, names, step => _log.LogDebug("Deriving {Step}", step));

        if (!layout.IsReady)
        {
            _log.LogError("Unreal reflection DISABLED: layout derivation failed at {Stage}: {Reason}. Plugins that use reflection will fail rather than read wrong memory.", layout.FailedAt, layout.FailureReason);
            runtime.Fail($"layout derivation failed at {layout.FailedAt}: {layout.FailureReason}");
            return;
        }

        Report(layout);

        _log.LogInformation("Unreal runtime ready: {Known}/{Declared} layout members, fingerprint {Fingerprint}", layout.Layout.Known, layout.Layout.Declared, layout.Fingerprint);

        runtime.Complete(memory, layout);

        ActivateSdk(runtime);

        if (_runtimeOptions.SelfCheck)
            UnrealSelfCheck.Run(_log, runtime);

        _plugins.StartAll(runtime);

        if (SdkRequest() is var (output, roots))
        {
            _log.LogInformation("SDK generation requested → {Directory}", output);
            SdkGenerator.Generate(runtime, output, roots, _log);
        }
    }

    private void ActivateSdk(UnrealRuntime runtime)
    {
        try
        {
            if (Type.GetType("Palforge.Sdk.SdkRegistry")?.GetMethod("Register", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) is { } register)
            {
                register.Invoke(null, [runtime]);
                _log.LogInformation("SDK: typed wrappers registered");
            }
        }
        catch (Exception exception)
        {
            _log.LogWarning(exception, "SDK: registry activation failed");
        }
    }

    private void Report(AnchorSet anchors)
    {
        _log.LogInformation("Engine version: {Version}", anchors.EngineVersion);
        _log.LogDebug("GUObjectArray: {Result}", anchors.GUObjectArray);
        _log.LogDebug("FName::FName: {Result}", anchors.FNameConstructor);
        _log.LogDebug("FName::ToString: {Result}", anchors.FNameToString);
        _log.LogDebug("GMalloc: {Result}", anchors.GMalloc);
        _log.LogDebug("UGameEngine::Tick: {Result}", anchors.GameEngineTick);
    }

    private void Report(UnrealLayout layout)
    {
        _log.LogDebug("Layout fingerprint: {Fingerprint}", layout.Fingerprint);

        foreach (var member in layout.Layout.Unverified())
            _log.LogDebug("Tabled (unverified against this build): {Member}", member);

        foreach (var conflict in layout.Conflicts)
            _log.LogWarning("Derived offset diverges from the stock table — this build is forked: {Conflict}", conflict);
    }

    private UnrealRuntime Disabled(string reason)
    {
        _log.LogError("Unreal reflection DISABLED: {Reason}. Plugins that use reflection will fail rather than read wrong memory.", reason);

        return UnrealRuntime.Disabled(reason);
    }
}