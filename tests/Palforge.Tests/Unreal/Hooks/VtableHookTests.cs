using Palforge.Memory;
using Palforge.Tests.Memory;
using Palforge.Unreal.Hooks;

namespace Palforge.Tests.Unreal.Hooks;

public sealed class VtableHookTests
{
    [Fact]
    public void InstallSwapsTheSlotAndDisposeRestoresIt()
    {
        var memory = new FakeMemory();
        var vtable = memory.Allocate(0x40, MemoryAccess.Read);
        var slot = vtable + (3 * nint.Size);

        var original = unchecked((nint)0x1111_2222_3333_4444);
        var detour = unchecked((nint)0x5555_6666_7777_8888);

        Assert.True(memory.WriteProtected(slot, original));

        Assert.True(VtableHook.TryInstall(memory, slot, detour, out var hook));
        Assert.Equal(original, hook.Original);

        Assert.True(memory.TryRead(slot, out nint afterInstall));
        Assert.Equal(detour, afterInstall);

        hook.Dispose();

        Assert.True(memory.TryRead(slot, out nint afterDispose));
        Assert.Equal(original, afterDispose);
    }

    [Fact]
    public void TryInstallFailsWhenTheSlotIsEmpty()
    {
        var memory = new FakeMemory();
        var vtable = memory.Allocate(0x40, MemoryAccess.Read);
        var slot = vtable + nint.Size;

        Assert.False(VtableHook.TryInstall(memory, slot, 0x1234, out _));
    }
}