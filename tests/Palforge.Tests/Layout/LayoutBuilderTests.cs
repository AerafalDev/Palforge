using Palforge.Layout;

namespace Palforge.Tests.Layout;

public sealed class LayoutBuilderTests
{
    [Fact]
    public void ADerivedMemberOutranksTheTable()
    {
        var layout = new LayoutBuilder()
            .Add(LayoutMember.Derived("A", 0x20))
            .AddTable("UE5.5", new Dictionary<string, int> { ["A"] = 0x10 })
            .Build();

        Assert.Equal(0x20, layout.OffsetOrThrow("A"));
        Assert.Equal(Provenance.Derived, layout["A"].Provenance);
    }

    [Fact]
    public void TheTableFillsWhatDerivationCouldNotDetermine()
    {
        var layout = new LayoutBuilder()
            .Add(LayoutMember.Undetermined("A", 2, "disagreed"))
            .AddTable("UE5.5", new Dictionary<string, int> { ["A"] = 0x10, ["B"] = 0x20 })
            .Build();

        Assert.Equal(0x10, layout.OffsetOrThrow("A"));
        Assert.Equal(0x20, layout.OffsetOrThrow("B"));
        Assert.Equal(Provenance.Tabled, layout["A"].Provenance);
    }

    [Fact]
    public void ADerivedMemberThatContradictsTheTableIsReportedAsAConflict()
    {
        var builder = new LayoutBuilder()
            .Add(LayoutMember.Derived("EnumNames", 0x48))
            .AddTable("UE5.5", new Dictionary<string, int> { ["EnumNames"] = 0x40 });

        Assert.Single(builder.Conflicts);
        Assert.Contains("0x48", builder.Conflicts[0], StringComparison.Ordinal);
        Assert.Contains("0x40", builder.Conflicts[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ADerivedMemberThatAgreesWithTheTableIsNotAConflict()
    {
        var builder = new LayoutBuilder()
            .Add(LayoutMember.Derived("A", 0x10))
            .AddTable("UE5.5", new Dictionary<string, int> { ["A"] = 0x10 });

        Assert.Empty(builder.Conflicts);
    }

    [Fact]
    public void TabledMembersLeaveTheLayoutCompleteButNotFullyVerified()
    {
        var layout = new LayoutBuilder()
            .Add(LayoutMember.Derived("A", 0x10))
            .AddTable("UE5.5", new Dictionary<string, int> { ["A"] = 0x10, ["B"] = 0x20 })
            .Build();

        Assert.True(layout.IsComplete);
        Assert.False(layout.IsFullyVerified);
        Assert.Single(layout.Unverified());
    }

    [Fact]
    public void MergingSeveralDerivedTablesKeepsEveryMember()
    {
        var first = new LayoutTable([LayoutMember.Derived("A", 0x10)]);
        var second = new LayoutTable([LayoutMember.Derived("B", 0x20)]);

        var layout = new LayoutBuilder().AddDerived(first).AddDerived(second).Build();

        Assert.Equal(2, layout.Declared);
        Assert.True(layout.IsFullyVerified);
    }
}