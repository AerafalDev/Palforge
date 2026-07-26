using Palforge.Memory;

namespace Palforge.Unreal.Hooks;

internal sealed class VtableHook : IDisposable
{
    private readonly IMemory _memory;
    private readonly nint _slot;
    private bool _installed;

    public nint Original { get; }

    private VtableHook(IMemory memory, nint slot, nint original)
    {
        _memory = memory;
        _slot = slot;

        Original = original;
        _installed = true;
    }

    public static bool TryInstall(IMemory memory, nint slot, nint detour, out VtableHook hook)
    {
        ArgumentNullException.ThrowIfNull(memory);

        hook = null!;

        if (slot is 0 || detour is 0 || !memory.TryRead(slot, out nint original) || original is 0)
            return false;

        if (!memory.WriteProtected(slot, detour))
            return false;

        hook = new VtableHook(memory, slot, original);

        return true;
    }

    public void Dispose()
    {
        if (!_installed)
            return;

        _installed = false;

        _memory.WriteProtected(_slot, Original);
    }
}