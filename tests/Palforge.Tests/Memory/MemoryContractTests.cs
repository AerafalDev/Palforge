using CsCheck;
using Palforge.Memory;

namespace Palforge.Tests.Memory;

public abstract class MemoryContractTests
{
    private protected abstract IMemory CreateMemory();

    [Fact]
    public void ReadReturnsWhatWasWritten()
    {
        using var scratch = new NativeScratch(1);

        var memory = CreateMemory();

        Assert.True(memory.TryWrite(scratch.Address + 8, 0x1122334455667788UL));
        Assert.True(memory.TryRead(scratch.Address + 8, out ulong value));
        Assert.Equal(0x1122334455667788UL, value);
    }

    [Fact]
    public void ReadOutsideAnyCommittedRegionFails()
    {
        var reserved = NativeScratch.ReserveWithoutCommitting(1);

        var memory = CreateMemory();

        Assert.False(memory.IsReadable(reserved, 8));
        Assert.False(memory.TryRead(reserved, out ulong _));
    }

    [Fact]
    public void ReadStraddlingIntoAnUnreadablePageFails()
    {
        using var scratch = new NativeScratch(2);

        scratch.Protect(1, PageProtection.NoAccess);

        var memory = CreateMemory();

        Assert.True(memory.IsReadable(scratch.PageAt(1) - 8, 8));
        Assert.False(memory.IsReadable(scratch.PageAt(1) - 4, 8));
        Assert.False(memory.TryRead(scratch.PageAt(1) - 4, out ulong _));
    }

    [Fact]
    public void NullIsNeverReadable()
    {
        var memory = CreateMemory();

        Assert.False(memory.IsReadable(0, 8));
        Assert.False(memory.TryRead(0, out ulong _));
    }

    [Fact]
    public void WriteToAReadOnlyPageFails()
    {
        using var scratch = new NativeScratch(1);

        scratch.Protect(0, PageProtection.ReadOnly);

        var memory = CreateMemory();

        Assert.False(memory.TryWrite(scratch.Address, 1UL));
        Assert.True(memory.TryRead(scratch.Address, out ulong value));
        Assert.Equal(0UL, value);
    }

    [Fact]
    public void ReadFromANoAccessPageFails()
    {
        using var scratch = new NativeScratch(1);

        scratch.Protect(0, PageProtection.NoAccess);

        var memory = CreateMemory();

        Assert.False(memory.IsReadable(scratch.Address, 8));
        Assert.False(memory.TryRead(scratch.Address, out ulong _));
    }

    [Fact]
    public void AnyValueSurvivesARoundTrip()
    {
        Gen.Int[0, NativeScratch.PageSize - 8].Select(Gen.ULong)
            .Sample((offset, written) =>
            {
                using var scratch = new NativeScratch(1);

                var memory = CreateMemory();

                return memory.TryWrite(scratch.Address + offset, written)
                    && memory.TryRead(scratch.Address + offset, out ulong read)
                    && read == written;
            });
    }
}