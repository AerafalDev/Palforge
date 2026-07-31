using CsCheck;
using Palforge.Signatures;
using Palforge.Tests.Memory;

namespace Palforge.Tests.Signatures;

public sealed class PatternScannerTests
{
    [Fact]
    public void MatchIsReportedAtItsStartByDefault()
    {
        byte[] data = [0x00, 0x11, 0x48, 0x8B, 0x05, 0x22, 0x33, 0x44, 0x55];

        Assert.Equal([2], PatternScanner.FindAll(data, 0, Pattern.Parse("48 8B 05 ?? ?? ?? ??")));
    }

    [Fact]
    public void CursorShiftsTheReportedAddressOntoTheDisplacement()
    {
        byte[] data = [0x00, 0x11, 0x48, 0x8B, 0x05, 0x22, 0x33, 0x44, 0x55];

        Assert.Equal([5], PatternScanner.FindAll(data, 0, Pattern.Parse("48 8B 05 | ?? ?? ?? ??")));
    }

    [Fact]
    public void BaseAddressIsAddedToEveryMatch()
    {
        byte[] data = [0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00];

        Assert.Equal([0x1000], PatternScanner.FindAll(data, 0x1000, Pattern.Parse("48 8B 05 ?? ?? ?? ??")));
    }

    [Fact]
    public void OverlappingMatchesAreAllReported()
    {
        byte[] data = [0x90, 0x90, 0x90, 0x90];

        Assert.Equal([0, 1, 2], PatternScanner.FindAll(data, 0, Pattern.Parse("90 90")));
    }

    [Fact]
    public void APatternRunningPastTheEndIsNotAMatch()
    {
        byte[] data = [0x00, 0x48, 0x8B, 0x05];

        Assert.Empty(PatternScanner.FindAll(data, 0, Pattern.Parse("48 8B 05 ?? ?? ?? ??")));
    }

    [Fact]
    public void AnchorFoundBeforeTheStartOfTheBufferIsNotAMatch()
    {
        byte[] data = [0x8B, 0x05, 0xE8, 0x00];

        Assert.Empty(PatternScanner.FindAll(data, 0, Pattern.Parse("48 8B 05 E8")));
    }

    [Fact]
    public void ChunkedScanFindsEveryMatchExactlyOnce()
    {
        var memory = new FakeMemory();
        const int length = 256 * 1024 * 3 + 777;
        var address = memory.Allocate(length);

        int[] positions = [0, 1024, 256 * 1024 - 16, 256 * 1024, 512 * 1024 + 11, length - 8];

        foreach (var position in positions)
            Assert.True(memory.TryWrite(address + position, 0x0000_0000_058B_48UL | (0xE8UL << 24)));

        var pattern = Pattern.Parse("48 8B 05 E8");
        var report = PatternScanner.Scan(memory, address, length, [pattern]);

        Assert.Equal([.. positions.Select(position => address + position)], report.MatchesOf(0));
    }

    [Fact]
    public void AMatchStraddlingAChunkBoundaryIsFoundOnce()
    {
        var memory = new FakeMemory();
        const int length = 256 * 1024 * 2;
        var address = memory.Allocate(length);

        const int boundary = 256 * 1024 - 2;

        Assert.True(memory.TryWrite(address + boundary, 0x0000_0000_058B_48UL | (0xE8UL << 24)));

        var report = PatternScanner.Scan(memory, address, length, [Pattern.Parse("48 8B 05 E8")]);

        Assert.Equal([address + boundary], report.MatchesOf(0));
    }

    [Fact]
    public void ScanAndSpanScanAgreeOnAnyBuffer()
    {
        var pattern = Pattern.Parse("48 8B 05 E8");

        Gen.Byte.Array[1024, 4096]
            .Sample(data =>
            {
                var memory = new FakeMemory();
                var address = memory.Allocate(data.Length);

                if (!memory.TryWrite(address, data))
                    return false;

                var chunked = PatternScanner.Scan(memory, address, data.Length, [pattern]).MatchesOf(0);
                var direct = PatternScanner.FindAll(data, address, pattern);

                return chunked.SequenceEqual(direct);
            });
    }

    [Fact]
    public void AnXrefMatchesOnlyWhenTheDisplacementResolvesToItsTarget()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(4096);

        var target = address + 0x800;
        var site = address + 0x100;

        Assert.True(memory.TryWrite(site, (byte)0x48));
        Assert.True(memory.TryWrite(site + 1, (byte)0x8D));
        Assert.True(memory.TryWrite(site + 2, (byte)0x15));
        Assert.True(memory.TryWrite(site + 3, (int)(target - (site + 7))));

        var right = Pattern.Parse($"48 8D 15 X0x{target:X}");
        var wrong = Pattern.Parse($"48 8D 15 X0x{target + 1:X}");

        Assert.Equal([site], PatternScanner.Scan(memory, address, 4096, [right]).MatchesOf(0));
        Assert.Empty(PatternScanner.Scan(memory, address, 4096, [wrong]).MatchesOf(0));
    }

    [Fact]
    public void AUtf16PatternFindsAWideStringLiteral()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(4096);

        Assert.True(memory.TryWrite(address + 64, System.Text.Encoding.Unicode.GetBytes("MovementComponent0\0")));

        var report = PatternScanner.Scan(memory, address, 4096, [Pattern.Utf16("MovementComponent0\0")]);

        Assert.Equal([address + 64], report.MatchesOf(0));
    }

    [Fact]
    public void ReportCountsHowManyPatternsMatched()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(4096);

        Assert.True(memory.TryWrite(address + 100, 0x0000_0000_058B_48UL | (0xE8UL << 24)));

        var report = PatternScanner.Scan(memory, address, 4096,
        [
            Pattern.Parse("48 8B 05 E8"),
            Pattern.Parse("11 22 33 44"),
        ]);

        Assert.Equal(2, report.Attempted);
        Assert.Equal(1, report.MatchedPatterns);
    }
}