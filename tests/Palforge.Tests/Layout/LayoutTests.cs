using Palforge.Layout;

namespace Palforge.Tests.Layout;

public sealed class LayoutTests
{
    [Fact]
    public void ALayoutWhereNothingWasAttemptedIsNotComplete()
    {
        var layout = new LayoutTable(
        [
            LayoutMember.NotAttempted("ClassPrivate", "the object array gate failed"),
            LayoutMember.NotAttempted("NamePrivate", "the object array gate failed")
        ]);

        Assert.False(layout.IsComplete);
        Assert.Equal(0, layout.Known);
        Assert.Equal(2, layout.NotAttempted);
    }

    [Fact]
    public void AnUndeterminedMemberYieldsNoOffset()
    {
        var member = LayoutMember.Undetermined("ClassPrivate", 3, "three candidates disagreed");

        Assert.False(member.TryGetOffset(out var offset));
        Assert.Equal(-1, offset);
        Assert.Throws<InvalidOperationException>(() => member.OffsetOrThrow());
    }

    [Fact]
    public void ANotAttemptedMemberYieldsNoOffset()
    {
        var member = LayoutMember.NotAttempted("ClassPrivate", "no anchor");

        Assert.False(member.TryGetOffset(out _));
        Assert.Throws<InvalidOperationException>(() => member.OffsetOrThrow());
    }

    [Fact]
    public void ZeroIsNeverHandedOutForAFailedMember()
    {
        LayoutMember[] failures =
        [
            LayoutMember.Undetermined("A", 0, "none"),
            LayoutMember.NotAttempted("B", "skipped")
        ];

        foreach (var member in failures)
        {
            Assert.False(member.TryGetOffset(out var offset));
            Assert.NotEqual(0, offset);
        }
    }

    [Fact]
    public void ATabledMemberIsKnownButNotVerified()
    {
        var member = LayoutMember.Tabled("ClassFlags", 0xD4, "UE5.5");

        Assert.True(member.IsKnown);
        Assert.False(member.IsVerified);
        Assert.True(member.TryGetOffset(out var offset));
        Assert.Equal(0xD4, offset);
    }

    [Fact]
    public void ADerivedMemberIsBothKnownAndVerified()
    {
        var member = LayoutMember.Derived("InternalIndex", 0x0C);

        Assert.True(member.IsKnown);
        Assert.True(member.IsVerified);
    }

    [Fact]
    public void CompletenessAndVerificationAreDifferentQuestions()
    {
        var layout = new LayoutTable(
        [
            LayoutMember.Derived("InternalIndex", 0x0C),
            LayoutMember.Tabled("ClassFlags", 0xD4, "UE5.5")
        ]);

        Assert.True(layout.IsComplete);
        Assert.False(layout.IsFullyVerified);
        Assert.Single(layout.Unverified());
        Assert.Empty(layout.Unusable());
    }

    [Fact]
    public void AnEmptyLayoutIsRejectedRatherThanReportedComplete()
    {
        Assert.Throws<ArgumentException>(() => new LayoutTable([]));
    }

    [Fact]
    public void AnUnknownMemberNameIsAnError()
    {
        var layout = new LayoutTable([LayoutMember.Derived("InternalIndex", 0x0C)]);

        Assert.Throws<KeyNotFoundException>(() => layout["NoSuchMember"]);
        Assert.False(layout.TryGetOffset("NoSuchMember", out _));
    }
}