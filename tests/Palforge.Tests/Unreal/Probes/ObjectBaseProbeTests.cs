using Palforge.Layout;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Unreal.Probes;

public sealed class ObjectBaseProbeTests
{
    [Fact]
    public void TheObjectBaseProbeDerivesEveryMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));
        Assert.True(layout.IsFullyVerified);

        Assert.Equal(graph.Layout.ClassPrivate, layout.OffsetOrThrow(LayoutNames.ClassPrivate));
        Assert.Equal(graph.Layout.NamePrivate, layout.OffsetOrThrow(LayoutNames.NamePrivate));
        Assert.Equal(graph.Layout.OuterPrivate, layout.OffsetOrThrow(LayoutNames.OuterPrivate));
        Assert.Equal(graph.Layout.ObjectFlags, layout.OffsetOrThrow(LayoutNames.ObjectFlags));
    }

    [Theory]
    [InlineData(0x08, 0x28, 0x30, 0x18)]
    [InlineData(0x20, 0x08, 0x30, 0x18)]
    [InlineData(0x30, 0x20, 0x08, 0x18)]
    public void TheObjectBaseProbeFollowsWhereverTheLayoutMoves(int classPrivate, int namePrivate, int outerPrivate, int objectFlags)
    {
        var graph = FakeObjectGraph.Build(new FakeLayout
        {
            ClassPrivate = classPrivate,
            NamePrivate = namePrivate,
            OuterPrivate = outerPrivate,
            ObjectFlags = objectFlags,
            InternalIndex = 0x1C
        });

        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(classPrivate, layout.OffsetOrThrow(LayoutNames.ClassPrivate));
        Assert.Equal(namePrivate, layout.OffsetOrThrow(LayoutNames.NamePrivate));
        Assert.Equal(outerPrivate, layout.OffsetOrThrow(LayoutNames.OuterPrivate));
        Assert.Equal(objectFlags, layout.OffsetOrThrow(LayoutNames.ObjectFlags));
    }

    [Fact]
    public void TheClassPointerIsFoundByItsFixedPointNotByAName()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph, new EmptyNameResolver());

        Assert.Equal(graph.Layout.ClassPrivate, layout.OffsetOrThrow(LayoutNames.ClassPrivate));
    }

    [Fact]
    public void WithoutNamesTheNameOffsetIsUndeterminedRatherThanGuessed()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph, new EmptyNameResolver());

        Assert.False(layout.IsComplete);
        Assert.False(layout[LayoutNames.NamePrivate].TryGetOffset(out _));
    }

    [Fact]
    public void AFailedNameLookupDoesNotSilentlyDegradeTheFlagsMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph, new EmptyNameResolver());

        Assert.Equal(Provenance.NotAttempted, layout[LayoutNames.ObjectFlags].Provenance);
        Assert.Contains("names", layout[LayoutNames.ObjectFlags].Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlagsMemberSeparatesDefaultObjectsFromOrdinaryOnes()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var offset = layout.OffsetOrThrow(LayoutNames.ObjectFlags);
        var name = layout.OffsetOrThrow(LayoutNames.NamePrivate);

        foreach (var address in graph.Objects)
        {
            Assert.True(graph.Memory.TryRead(address + offset, out uint flags));
            Assert.True(graph.Memory.TryRead(address + name, out int id));
            Assert.True(graph.Names.TryResolve(id, out var resolved));

            var isDefault = resolved.StartsWith("Default__", StringComparison.Ordinal);

            Assert.Equal(isDefault, (flags & FakeObjectGraph.ClassDefaultObjectFlag) is not 0);
        }
    }

    private static LayoutTable Probe(FakeObjectGraph graph, INameResolver? names = null)
    {
        var gate = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, gate, out var view));

        return new ObjectBaseProbe(graph.Memory, view, names ?? graph.Names).Probe();
    }
}