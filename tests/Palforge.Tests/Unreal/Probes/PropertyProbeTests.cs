using Palforge.Layout;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Unreal.Probes;

public sealed class PropertyProbeTests
{
    [Fact]
    public void ThePropertyProbeDerivesEveryMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));
        Assert.True(layout.IsFullyVerified);

        Assert.Equal(graph.Layout.FFieldNext, layout.OffsetOrThrow(LayoutNames.FieldChainNext));
        Assert.Equal(graph.Layout.FFieldNamePrivate, layout.OffsetOrThrow(LayoutNames.FieldNamePrivate));
        Assert.Equal(graph.Layout.FFieldOwner, layout.OffsetOrThrow(LayoutNames.FieldOwner));
        Assert.Equal(graph.Layout.FFieldClassPrivate, layout.OffsetOrThrow(LayoutNames.FieldClassPrivate));
        Assert.Equal(graph.Layout.FieldClassName, layout.OffsetOrThrow(LayoutNames.FieldClassName));
        Assert.Equal(graph.Layout.ArrayDim, layout.OffsetOrThrow(LayoutNames.ArrayDim));
        Assert.Equal(graph.Layout.ElementSize, layout.OffsetOrThrow(LayoutNames.ElementSize));
        Assert.Equal(graph.Layout.OffsetInternal, layout.OffsetOrThrow(LayoutNames.OffsetInternal));
    }

    [Fact]
    public void TheSubclassSlotIsDerivedRatherThanAsserted()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        Assert.Equal(graph.Layout.PropertyBaseSize, layout.OffsetOrThrow(LayoutNames.PropertyBaseSize));
        Assert.Equal(Provenance.Derived, layout[LayoutNames.PropertyBaseSize].Provenance);
    }

    [Fact]
    public void ThePropertyProbeFollowsAFieldBlockPushedForward()
    {
        AssertFollows(new FakeLayout
        {
            FFieldClassPrivate = 0x08,
            FFieldOwner = 0x20,
            FFieldFlags = 0x28,
            FFieldNext = 0x30,
            FFieldNamePrivate = 0x38,
            ArrayDim = 0x48,
            ElementSize = 0x4C,
            PropertyFlags = 0x50,
            RepIndex = 0x58,
            OffsetInternal = 0x5C,
            PropertyLinkNext = 0x60,
            PropertyBaseSize = 0x90
        });
    }

    [Fact]
    public void ThePropertyProbeFollowsAFieldBlockPulledBack()
    {
        AssertFollows(new FakeLayout
        {
            FFieldOwner = 0x10,
            FFieldClassPrivate = 0x18,
            FFieldNext = 0x20,
            FFieldNamePrivate = 0x28,
            FFieldFlags = 0x30,
            ArrayDim = 0x38,
            ElementSize = 0x3C,
            PropertyFlags = 0x40,
            RepIndex = 0x48,
            OffsetInternal = 0x4C,
            PropertyLinkNext = 0x50,
            PropertyBaseSize = 0xA0
        });
    }

    private static void AssertFollows(FakeLayout shifted)
    {
        var graph = FakeObjectGraph.Build(shifted);
        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(shifted.FFieldNext, layout.OffsetOrThrow(LayoutNames.FieldChainNext));
        Assert.Equal(shifted.FFieldNamePrivate, layout.OffsetOrThrow(LayoutNames.FieldNamePrivate));
        Assert.Equal(shifted.FFieldOwner, layout.OffsetOrThrow(LayoutNames.FieldOwner));
        Assert.Equal(shifted.FFieldClassPrivate, layout.OffsetOrThrow(LayoutNames.FieldClassPrivate));
        Assert.Equal(shifted.OffsetInternal, layout.OffsetOrThrow(LayoutNames.OffsetInternal));
        Assert.Equal(shifted.ElementSize, layout.OffsetOrThrow(LayoutNames.ElementSize));
        Assert.Equal(shifted.ArrayDim, layout.OffsetOrThrow(LayoutNames.ArrayDim));
        Assert.Equal(shifted.PropertyBaseSize, layout.OffsetOrThrow(LayoutNames.PropertyBaseSize));
    }

    [Fact]
    public void TheChainIsProvenByItsMemberNamesNotByItsLength()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var next = layout.OffsetOrThrow(LayoutNames.FieldChainNext);
        var name = layout.OffsetOrThrow(LayoutNames.FieldNamePrivate);

        Assert.True(graph.Memory.TryRead(graph.ClassNamed("Vector") + graph.Layout.ChildProperties, out nint field));

        var members = new List<string>();

        while (field is not 0)
        {
            Assert.True(graph.Memory.TryRead(field + name, out int id));
            Assert.True(graph.Names.TryResolve(id, out var resolved));

            members.Add(resolved);

            Assert.True(graph.Memory.TryRead(field + next, out field));
        }

        Assert.Equal(["X", "Y", "Z"], members);
    }

    [Fact]
    public void WithoutNamesNothingIsAttempted()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph, new EmptyNameResolver());

        Assert.False(layout.IsComplete);

        foreach (var member in layout.Members)
            Assert.False(member.TryGetOffset(out _));
    }

    private static LayoutTable Probe(FakeObjectGraph graph, INameResolver? names = null)
    {
        var resolver = names ?? graph.Names;

        var gate = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, gate, out var view));

        var objectBase = new ObjectBaseProbe(graph.Memory, view, graph.Names).Probe();
        var structs = new StructProbe(graph.Memory, view, graph.Names, objectBase).Probe();

        var known = new LayoutBuilder().AddDerived(objectBase).AddDerived(structs).Build();

        return new PropertyProbe(graph.Memory, view, resolver, known).Probe();
    }
}