using CsCheck;
using Palforge.Memory;

namespace Palforge.Tests.Memory;

public sealed class RegionMapTests
{
    [Fact]
    public void LookupFindsTheRegionContainingAnAddress()
    {
        var map = new RegionMap(
        [
            new MemoryRegion(0x2000, 0x1000, MemoryAccess.Read),
            new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read),
            new MemoryRegion(0x4000, 0x1000, MemoryAccess.Read)
        ]);

        Assert.True(map.TryGetRegion(0x2500, out var region));
        Assert.Equal(0x2000, region.Address);
    }

    [Fact]
    public void LookupFailsInsideAHole()
    {
        var map = new RegionMap(
        [
            new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read),
            new MemoryRegion(0x4000, 0x1000, MemoryAccess.Read)
        ]);

        Assert.False(map.TryGetRegion(0x3000, out _));
        Assert.False(map.IsReadable(0x3000, 8));
    }

    [Fact]
    public void ReadSpanningTwoAdjacentReadableRegionsIsAllowed()
    {
        var map = new RegionMap(
        [
            new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read),
            new MemoryRegion(0x2000, 0x1000, MemoryAccess.Read)
        ]);

        Assert.True(map.IsReadable(0x1FFC, 8));
    }

    [Fact]
    public void ReadSpanningIntoANonReadableRegionIsRejected()
    {
        var map = new RegionMap(
        [
            new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read),
            new MemoryRegion(0x2000, 0x1000, MemoryAccess.Execute)
        ]);

        Assert.True(map.IsReadable(0x1FF0, 8));
        Assert.False(map.IsReadable(0x1FFC, 8));
    }

    [Fact]
    public void ReadRunningOffTheEndOfTheLastRegionIsRejected()
    {
        var map = new RegionMap([new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read)]);

        Assert.False(map.IsReadable(0x1FFC, 8));
    }

    [Fact]
    public void NullIsNeverReadable()
    {
        var map = new RegionMap([new MemoryRegion(0x1000, 0x1000, MemoryAccess.Read)]);

        Assert.False(map.IsReadable(0, 8));
    }

    [Fact]
    public void LookupAgreesWithALinearScanOverAnyLayout()
    {
        Gen.Int[1, 32]
            .SelectMany(static count => Gen.Int[1, 64].Array[count])
            .Select(static gaps =>
            {
                var regions = new List<MemoryRegion>(gaps.Length);
                nint address = 0x1000;

                foreach (var gap in gaps)
                {
                    regions.Add(new MemoryRegion(address, 0x1000, MemoryAccess.Read));
                    address += 0x1000 + gap * 0x1000;
                }

                return regions;
            })
            .Sample(static regions =>
            {
                var map = new RegionMap(regions);

                for (nint address = 0x1000; address < 0x1000 + 0x1000 * 256; address += 0x400)
                {
                    var expected = regions.Any(region => region.Contains(address));

                    if (map.TryGetRegion(address, out _) != expected)
                        return false;
                }

                return true;
            });
    }
}