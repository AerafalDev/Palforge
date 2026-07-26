using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Palforge.Layout;
using Palforge.Memory;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Threading;

namespace Palforge.Unreal.Hooks;

internal sealed unsafe class GameThreadPump
{
    private const int PollIntervalMs = 250;
    private const int WarmupObjectThreshold = 30000;
    private const int MaxWarmupTicks = 3000;
    private const int MaxVtableSlots = 512;
    private const int MaxSuperDepth = 64;
    private const uint ClassDefaultObjectFlag = 0x10;

    private static GameThreadPump? s_current;

    private readonly nint _objectArray;
    private readonly LayoutTable _table;
    private readonly INameResolver _names;
    private readonly nint _tickAddress;
    private readonly ILogger _log;
    private readonly Action _onWarmedUp;
    private readonly ConcurrentQueue<Action> _actions;

    private readonly int _objectFlags;
    private readonly int _classPrivate;
    private readonly int _namePrivate;
    private readonly int _superStruct;
    private readonly int _classDefaultObject;
    private readonly int _numElements;

    private DirectMemory _memory = null!;
    private delegate* unmanaged<nint, float, byte, void> _original;
    private volatile bool _warmedUp;
    private int _ticks;

    public Action? Heartbeat { get; set; }

    public GameThreadPump(nint objectArray, LayoutTable table, INameResolver names, nint tickAddress, ILogger log, Action onWarmedUp)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(onWarmedUp);

        _actions = [];
        _objectArray = objectArray;
        _table = table;
        _names = names;
        _tickAddress = tickAddress;
        _log = log;
        _onWarmedUp = onWarmedUp;

        _objectFlags = table.OffsetOrThrow(LayoutNames.ObjectFlags);
        _classPrivate = table.OffsetOrThrow(LayoutNames.ClassPrivate);
        _namePrivate = table.OffsetOrThrow(LayoutNames.NamePrivate);
        _superStruct = table.OffsetOrThrow(LayoutNames.SuperStruct);
        _classDefaultObject = table.OffsetOrThrow(LayoutNames.ClassDefaultObject);
        _numElements = table.OffsetOrThrow(LayoutNames.NumElements);
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _actions.Enqueue(action);
    }

    public void StartInstalling()
    {
        new Thread(InstallLoop)
        {
            Name = "Palforge.GameThread.Install",
            IsBackground = true
        }.Start();
    }

    private void InstallLoop()
    {
        while (!TryInstall())
            Thread.Sleep(PollIntervalMs);
    }

    private bool TryInstall()
    {
        var memory = new DirectMemory(RegionMap.FromCurrentProcess());

        if (!ObjectArrayView.TryCreate(memory, _objectArray, _table, out var view))
            return false;

        if (!TryFindEngineClass(memory, view, out var engineClass)
            || !TryFindTickSlot(memory, engineClass, out var index)
            || !TryFindLiveEngine(memory, view, engineClass, out var engine))
            return false;

        if (!memory.TryRead(engine, out nint vtable) || vtable is 0)
            return false;

        var slot = vtable + ((nint)index * nint.Size);

        if (!VtableHook.TryInstall(memory, slot, (nint)(delegate* unmanaged<nint, float, byte, void>)&Detour, out var hook))
            return false;

        _memory = memory;
        _original = (delegate* unmanaged<nint, float, byte, void>)hook.Original;
        s_current = this;

        _log.LogInformation("Game-thread pump installed on the live engine (Tick vtable slot {Index})", index);

        return true;
    }

    private bool TryFindEngineClass(DirectMemory memory, ObjectArrayView view, out nint engineClass)
    {
        engineClass = 0;

        if (!TryFindMetaClass(memory, view, out var metaClass)
            || !_names.TryFind("GameEngine", out var wanted) || wanted is 0)
            return false;

        foreach (var address in view.Addresses())
        {
            if (memory.TryRead(address + _namePrivate, out int id) && id == wanted
                && memory.TryRead(address + _classPrivate, out nint klass) && klass == metaClass)
            {
                engineClass = address;
                return true;
            }
        }

        return false;
    }

    private bool TryFindMetaClass(DirectMemory memory, ObjectArrayView view, out nint metaClass)
    {
        metaClass = 0;

        if (!_names.TryFind("Class", out var classId) || classId is 0)
            return false;

        foreach (var address in view.Addresses())
        {
            if (memory.TryRead(address + _namePrivate, out int id) && id == classId
                && memory.TryRead(address + _classPrivate, out nint klass) && klass == address)
            {
                metaClass = address;
                return true;
            }
        }

        return false;
    }

    private bool TryFindTickSlot(DirectMemory memory, nint engineClass, out int index)
    {
        index = -1;

        if (!memory.TryRead(engineClass + _classDefaultObject, out nint cdo) || cdo is 0
            || !memory.TryRead(cdo, out nint vtable) || vtable is 0)
            return false;

        for (var slot = 0; slot < MaxVtableSlots; slot++)
        {
            if (memory.TryRead(vtable + ((nint)slot * nint.Size), out nint entry) && entry == _tickAddress)
            {
                index = slot;
                return true;
            }
        }

        return false;
    }

    private bool TryFindLiveEngine(DirectMemory memory, ObjectArrayView view, nint engineClass, out nint engine)
    {
        engine = 0;

        foreach (var address in view.Addresses())
        {
            if (memory.TryRead(address + _objectFlags, out uint flags) && (flags & ClassDefaultObjectFlag) is not 0)
                continue;

            if (memory.TryRead(address + _classPrivate, out nint klass) && DerivesFrom(memory, klass, engineClass))
            {
                engine = address;
                return true;
            }
        }

        return false;
    }

    private bool DerivesFrom(DirectMemory memory, nint klass, nint target)
    {
        for (var depth = 0; klass is not 0 && depth < MaxSuperDepth; depth++)
        {
            if (klass == target)
                return true;

            if (!memory.TryRead(klass + _superStruct, out klass))
                return false;
        }

        return false;
    }

    [UnmanagedCallersOnly]
    private static void Detour(nint self, float delta, byte idle)
    {
        var pump = s_current;

        if (pump is null)
            return;

        pump._original(self, delta, idle);
        pump.OnTick();
    }

    private void OnTick()
    {
        if (!GameThread.IsCurrent)
        {
            GameThread.MarkCurrent();
            SynchronizationContext.SetSynchronizationContext(new GameThreadContext());
        }

        while (_actions.TryDequeue(out var action))
            Run(action);

        if (Heartbeat is { } heartbeat)
            Run(heartbeat);

        if (_warmedUp)
            return;

        _ticks++;

        var objects = ObjectCount();

        if (objects < WarmupObjectThreshold && _ticks < MaxWarmupTicks)
            return;

        _warmedUp = true;

        _log.LogInformation("Game thread warmed up ({Objects} objects, {Ticks} ticks) — deriving the layout now", objects, _ticks);

        Run(_onWarmedUp);
    }

    private int ObjectCount()
    {
        return _memory.TryRead(_objectArray + _numElements, out int count) ? count : 0;
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "A game-thread action threw.");
        }
    }
}