using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Palforge.Memory;
using Palforge.Unreal.Hooks;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Scheduler;
using Palforge.Unreal.Stage;
using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Runtime;

internal sealed class UnrealRuntime
{
    private readonly Lock _gate;
    private readonly List<Action> _tick;
    private readonly ILogger _log;

    private volatile UnrealRuntimeState _state;
    private string? _reason;
    private IMemory? _memory;
    private UnrealLayout? _layout;
    private HookManager? _hooks;
    private TickScheduler? _scheduler;

    public AnchorSet? Anchors { get; }

    public bool IsReady =>
        _state is UnrealRuntimeState.Ready;

    public bool IsArming =>
        _state is UnrealRuntimeState.Arming;

    public bool IsDisabled =>
        _state is UnrealRuntimeState.Disabled;

    public string? DisabledReason =>
        _reason;

    public IMemory Memory =>
        _memory ?? throw NotReady();

    public UnrealLayout Layout =>
        _layout ?? throw NotReady();

    internal UnrealContext Reflection
    {
        get
        {
            if (_state is not UnrealRuntimeState.Ready)
                throw NotReady();

            lock (_gate)
                return field ??= new UnrealContext(_memory!, _layout!.Layout, _layout.Names, _layout.Objects, Anchors?.GMalloc.TryGetValue(out var resolved) is true ? resolved : 0);
        }
    }

    internal HookManager Hooks
    {
        get
        {
            if (_state is not UnrealRuntimeState.Ready)
                throw NotReady();

            lock (_gate)
                return _hooks ??= new HookManager(Reflection, _log);
        }
    }

    internal TickScheduler Scheduler
    {
        get
        {
            lock (_gate)
                return _scheduler ??= new TickScheduler(_log);
        }
    }

    private UnrealRuntime(UnrealRuntimeState state, AnchorSet? anchors, string? reason, ILogger log)
    {
        _state = state;
        _reason = reason;
        _log = log;
        _gate = new Lock();
        _tick = [];

        Anchors = anchors;
    }

    public static UnrealRuntime Arming(AnchorSet anchors, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(log);

        return new UnrealRuntime(UnrealRuntimeState.Arming, anchors, null, log);
    }

    public static UnrealRuntime Disabled(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new UnrealRuntime(UnrealRuntimeState.Disabled, null, reason, NullLogger.Instance);
    }

    public void Complete(IMemory memory, UnrealLayout layout)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(layout);

        lock (_gate)
        {
            _memory = memory;
            _layout = layout;
            _reason = null;
            _state = UnrealRuntimeState.Ready;
        }
    }

    internal IDisposable AddTickCallback(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_gate)
            _tick.Add(callback);

        return new TickRegistration(this, callback);
    }

    internal void RemoveTickCallback(Action callback)
    {
        lock (_gate)
            _tick.Remove(callback);
    }

    internal void OnGameThreadTick()
    {
        _hooks?.OnGameThreadTick();
        _scheduler?.Tick();

        Action[] callbacks;

        lock (_gate)
        {
            callbacks = _tick.Count is 0 ? [] : [.. _tick];
        }

        foreach (var callback in callbacks)
            GameThreadGuard.Run(callback, _log, "A tick callback");
    }

    public void Fail(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_gate)
        {
            _reason = reason;
            _state = UnrealRuntimeState.Disabled;
        }
    }

    private InvalidOperationException NotReady()
    {
        return new InvalidOperationException($"The Unreal runtime is not ready ({_state}): {_reason}".TrimEnd());
    }
}