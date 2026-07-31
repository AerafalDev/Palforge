using Palforge.Commands.Arguments;
using Palforge.Commands.Modules;

namespace Palforge.Tests.Commands;

public sealed class ArgumentReaderRegistryTests
{
    private static readonly TestCommandContext s_context = new TestCommandContext();

    [Fact]
    public void AReadsASeededPrimitive()
    {
        var reader = new ArgumentReaderRegistry().Resolve(typeof(int));

        Assert.NotNull(reader);
        Assert.True(reader.TryRead(s_context, "42".AsSpan(), out var value, out _));
        Assert.Equal(42, value);
    }

    [Theory]
    [InlineData("Monday")]
    [InlineData("monday")]
    [InlineData("1")]
    public void BBuildsAnEnumReaderByNameOrNumber(string input)
    {
        var reader = new ArgumentReaderRegistry().Resolve(typeof(DayOfWeek));

        Assert.NotNull(reader);
        Assert.True(reader.TryRead(s_context, input.AsSpan(), out var value, out _));
        Assert.Equal(DayOfWeek.Monday, value);
    }

    [Fact]
    public void CRejectsAnInvalidEnumValueWithTheChoices()
    {
        var reader = new ArgumentReaderRegistry().Resolve(typeof(DayOfWeek))!;

        Assert.False(reader.TryRead(s_context, "Nonsuch".AsSpan(), out _, out var error));
        Assert.Contains("Monday", error);
    }

    [Fact]
    public void DBuildsANullableReaderThatAcceptsNull()
    {
        var reader = new ArgumentReaderRegistry().Resolve(typeof(int?))!;

        Assert.True(reader.TryRead(s_context, "null".AsSpan(), out var none, out _));
        Assert.Null(none);
        Assert.True(reader.TryRead(s_context, "5".AsSpan(), out var some, out _));
        Assert.Equal(5, some);
    }

    [Fact]
    public void EACustomReaderOverridesTheBuiltIn()
    {
        var registry = new ArgumentReaderRegistry();
        registry.Register(new FixedIntReader(99));

        var reader = registry.Resolve(typeof(int))!;

        Assert.True(reader.TryRead(s_context, "42".AsSpan(), out var value, out _));
        Assert.Equal(99, value);
    }

    [Fact]
    public void FAnUnreadableTypeHasNoReader()
    {
        Assert.Null(new ArgumentReaderRegistry().Resolve(typeof(ArgumentReaderRegistryTests)));
    }

    private sealed class FixedIntReader : ArgumentReader<int>
    {
        private readonly int _value;

        public FixedIntReader(int value)
        {
            _value = value;
        }

        public override bool TryRead(CommandContext context, ReadOnlySpan<char> input, out int value, out string? errorMessage)
        {
            value = _value;
            errorMessage = null;

            return true;
        }
    }
}