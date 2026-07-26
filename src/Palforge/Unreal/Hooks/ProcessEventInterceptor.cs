using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Palforge.Memory;

namespace Palforge.Unreal.Hooks;

internal sealed unsafe class ProcessEventInterceptor
{
    private static ProcessEventInterceptor? s_current;

    private readonly IMemory _memory;
    private readonly int _slot;
    private readonly nint _detour;
    private readonly ConcurrentDictionary<nint, nint> _originals;
    private readonly Func<nint, nint, nint, HookContext?> _before;
    private readonly Action<HookContext> _after;

    public ProcessEventInterceptor(IMemory memory, int processEventByteOffset, Func<nint, nint, nint, HookContext?> before, Action<HookContext> after)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        _originals = [];
        _memory = memory;
        _slot = processEventByteOffset;
        _before = before;
        _after = after;
        _detour = (nint)(delegate* unmanaged<nint, nint, nint, void>)&Detour;
        s_current = this;
    }

    public bool InstallOn(nint vtable)
    {
        if (vtable is 0)
            return false;

        if (_originals.ContainsKey(vtable))
            return true;

        if (!_memory.TryRead(vtable + _slot, out nint original) || original is 0 || original == _detour)
            return original == _detour;

        _originals[vtable] = original;

        if (_memory.WriteProtected(vtable + _slot, _detour))
            return true;

        _originals.TryRemove(vtable, out _);

        return false;
    }

    [UnmanagedCallersOnly]
    private static void Detour(nint self, nint function, nint parms)
    {
        s_current?.Dispatch(self, function, parms);
    }

    private void Dispatch(nint self, nint function, nint parms)
    {
        var original = self is not 0 && _originals.TryGetValue(*(nint*)self, out var resolved) ? resolved : 0;

        HookContext? context = null;

        try
        {
            context = _before(self, function, parms);
        }
        catch
        {
            // ignore
        }

        if ((context is null || !context.OriginalSkipped) && original is not 0)
            ((delegate* unmanaged<nint, nint, nint, void>)original)(self, function, parms);

        if (context is null)
            return;

        try
        {
            _after(context);
        }
        catch
        {
            // ignore
        }
    }
}