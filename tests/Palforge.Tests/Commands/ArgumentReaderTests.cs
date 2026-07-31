using Palforge.Commands.Arguments;

namespace Palforge.Tests.Commands;

public sealed class ArgumentReaderTests
{
    [Fact]
    public void ASplitsOnWhitespace()
    {
        var reader = new RawArgumentReader("give Aerafal 5".AsSpan());

        Assert.True(reader.TryReadToken(out var first));
        Assert.Equal("give", first.ToString());
        Assert.True(reader.TryReadToken(out var second));
        Assert.Equal("Aerafal", second.ToString());
        Assert.True(reader.TryReadToken(out var third));
        Assert.Equal("5", third.ToString());
        Assert.False(reader.TryReadToken(out _));
    }

    [Fact]
    public void BKeepsAQuotedRunTogether()
    {
        var reader = new RawArgumentReader("say \"hello world\" now".AsSpan());

        reader.TryReadToken(out var say);
        reader.TryReadToken(out var quoted);
        reader.TryReadToken(out var now);

        Assert.Equal("say", say.ToString());
        Assert.Equal("hello world", quoted.ToString());
        Assert.Equal("now", now.ToString());
    }

    [Fact]
    public void CCollapsesRunsOfWhitespace()
    {
        var reader = new RawArgumentReader("  a    b  ".AsSpan());

        reader.TryReadToken(out var a);
        reader.TryReadToken(out var b);

        Assert.Equal("a", a.ToString());
        Assert.Equal("b", b.ToString());
        Assert.False(reader.TryReadToken(out _));
    }

    [Fact]
    public void DRemainderTakesTheRestTrimmed()
    {
        var reader = new RawArgumentReader("echo   hello   world  ".AsSpan());

        reader.TryReadToken(out _);

        Assert.Equal("hello   world", reader.ReadRemainder().ToString());
    }

    [Fact]
    public void EEmptyInputYieldsNothing()
    {
        var reader = new RawArgumentReader("   ".AsSpan());

        Assert.False(reader.TryReadToken(out _));
        Assert.True(reader.ReadRemainder().IsEmpty);
    }
}