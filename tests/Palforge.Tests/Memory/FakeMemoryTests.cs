using CsCheck;
using Palforge.Memory;

namespace Palforge.Tests.Memory;

public sealed class FakeMemoryTests
{
    [Fact]
    public void ReadReturnsWhatWasWritten()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(64);

        Assert.True(memory.TryWrite(address + 8, 0x1122334455667788UL));
        Assert.True(memory.TryRead(address + 8, out ulong value));
        Assert.Equal(0x1122334455667788UL, value);
    }

    [Fact]
    public void ReadOutsideAnyRegionFails()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(64);

        Assert.False(memory.TryRead(address + 4096, out ulong _));
        Assert.False(memory.IsReadable(address + 4096, 8));
    }

    [Fact]
    public void ReadStraddlingTheEndOfARegionFails()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(64);

        Assert.True(memory.TryRead(address + 56, out ulong _));
        Assert.False(memory.TryRead(address + 57, out ulong _));
    }

    [Fact]
    public void NullIsNeverReadable()
    {
        var memory = new FakeMemory();

        Assert.False(memory.IsReadable(0, 8));
        Assert.False(memory.TryRead(0, out ulong _));
    }

    [Fact]
    public void WriteToAReadOnlyRegionFails()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(64, MemoryAccess.Read);

        Assert.False(memory.TryWrite(address, 1UL));
        Assert.True(memory.TryRead(address, out ulong value));
        Assert.Equal(0UL, value);
    }

    [Fact]
    public void ReadFromAnExecuteOnlyRegionFails()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(64, MemoryAccess.Execute);

        Assert.False(memory.IsReadable(address, 8));
        Assert.False(memory.TryRead(address, out ulong _));
    }

    [Fact]
    public void AnyValueSurvivesARoundTrip()
    {
        Gen.Int[0, 4096 - 8].Select(Gen.ULong)
            .Sample(static (offset, written) =>
            {
                var memory = new FakeMemory();
                var address = memory.Allocate(4096);

                return memory.TryWrite(address + offset, written)
                    && memory.TryRead(address + offset, out ulong read)
                    && read == written;
            });
    }
}