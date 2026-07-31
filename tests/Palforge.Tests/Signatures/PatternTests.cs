using Palforge.Signatures;

namespace Palforge.Tests.Signatures;

public sealed class PatternTests
{
    [Fact]
    public void LiteralBytesAreParsed()
    {
        var pattern = Pattern.Parse("48 8B 05");

        Assert.Equal(3, pattern.Length);
        Assert.True(pattern.Matches([0x48, 0x8B, 0x05], 0));
        Assert.False(pattern.Matches([0x48, 0x8B, 0x06], 0));
    }

    [Fact]
    public void WildcardsMatchAnyByte()
    {
        var pattern = Pattern.Parse("48 ?? 05");

        Assert.True(pattern.Matches([0x48, 0x00, 0x05], 0));
        Assert.True(pattern.Matches([0x48, 0xFF, 0x05], 0));
        Assert.False(pattern.Matches([0x49, 0xFF, 0x05], 0));
    }

    [Fact]
    public void HighNibbleWildcardConstrainsOnlyTheLowNibble()
    {
        var pattern = Pattern.Parse("?8 05");

        Assert.True(pattern.Matches([0x48, 0x05], 0));
        Assert.True(pattern.Matches([0x88, 0x05], 0));
        Assert.False(pattern.Matches([0x49, 0x05], 0));
    }

    [Fact]
    public void LowNibbleWildcardConstrainsOnlyTheHighNibble()
    {
        var pattern = Pattern.Parse("4? 05");

        Assert.True(pattern.Matches([0x48, 0x05], 0));
        Assert.True(pattern.Matches([0x4C, 0x05], 0));
        Assert.False(pattern.Matches([0x58, 0x05], 0));
    }

    [Fact]
    public void CursorDefaultsToTheStartOfTheMatch()
    {
        Assert.Equal(0, Pattern.Parse("48 8B 05 ?? ?? ?? ??").Cursor);
    }

    [Fact]
    public void CursorMarksWhereTheResultIsReported()
    {
        Assert.Equal(3, Pattern.Parse("48 8B 05 | ?? ?? ?? ??").Cursor);
    }

    [Fact]
    public void AnchorIsTheLongestLiteralRun()
    {
        var pattern = Pattern.Parse("48 ?? 8B 05 E8 ?? 90");

        Assert.Equal(3, pattern.AnchorLength);
        Assert.True(pattern.Anchor.SequenceEqual<byte>([0x8B, 0x05, 0xE8]));
    }

    [Fact]
    public void AnchorSkipsNibbleWildcardsBecauseTheyAreNotLiteral()
    {
        var pattern = Pattern.Parse("4? 8B 05 E8");

        Assert.Equal(1, pattern.AnchorOffset);
        Assert.Equal(3, pattern.AnchorLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("48 ZZ 05")]
    [InlineData("48 8B5")]
    [InlineData("?? ?? ??")]
    public void MalformedPatternsAreRejected(string text)
    {
        Assert.False(Pattern.TryParse(text, out _));
        Assert.Throws<FormatException>(() => Pattern.Parse(text));
    }
}