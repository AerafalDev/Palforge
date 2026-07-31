using Palforge.Layout;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Unreal.Probes;

public sealed class StructProbeTests
{
    [Fact]
    public void TheStructProbeDerivesEveryMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));
        Assert.True(layout.IsFullyVerified);

        Assert.Equal(graph.Layout.SuperStruct, layout.OffsetOrThrow(LayoutNames.SuperStruct));
        Assert.Equal(graph.Layout.PropertiesSize, layout.OffsetOrThrow(LayoutNames.PropertiesSize));
        Assert.Equal(graph.Layout.MinAlignment, layout.OffsetOrThrow(LayoutNames.MinAlignment));
        Assert.Equal(graph.Layout.ChildProperties, layout.OffsetOrThrow(LayoutNames.ChildProperties));
        Assert.Equal(graph.Layout.Children, layout.OffsetOrThrow(LayoutNames.Children));
        Assert.Equal(graph.Layout.FieldNext, layout.OffsetOrThrow(LayoutNames.FieldNext));
        Assert.Equal(graph.Layout.ClassDefaultObject, layout.OffsetOrThrow(LayoutNames.ClassDefaultObject));
    }

    [Theory]
    [InlineData(0x58, 0x68, 0x78, 0x88)]
    [InlineData(0x90, 0xA0, 0xB0, 0xC0)]
    public void TheStructProbeFollowsWhereverTheLayoutMoves(int superStruct, int children, int childProperties, int propertiesSize)
    {
        var graph = FakeObjectGraph.Build(new FakeLayout
        {
            SuperStruct = superStruct,
            Children = children,
            ChildProperties = childProperties,
            PropertiesSize = propertiesSize,
            MinAlignment = propertiesSize + 4
        });

        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(superStruct, layout.OffsetOrThrow(LayoutNames.SuperStruct));
        Assert.Equal(children, layout.OffsetOrThrow(LayoutNames.Children));
        Assert.Equal(childProperties, layout.OffsetOrThrow(LayoutNames.ChildProperties));
        Assert.Equal(propertiesSize, layout.OffsetOrThrow(LayoutNames.PropertiesSize));
    }

    [Fact]
    public void TheChildrenChainIsProvenByOwnershipNotByShape()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var children = layout.OffsetOrThrow(LayoutNames.Children);
        var next = layout.OffsetOrThrow(LayoutNames.FieldNext);
        var outer = graph.Layout.OuterPrivate;

        var owner = graph.ClassNamed("Class");

        Assert.True(graph.Memory.TryRead(owner + children, out nint element));

        var walked = 0;

        while (element is not 0)
        {
            Assert.True(graph.Memory.TryRead(element + outer, out nint holder));
            Assert.Equal(owner, holder);

            walked++;

            Assert.True(graph.Memory.TryRead(element + next, out element));
        }

        Assert.Equal(3, walked);
    }

    [Fact]
    public void PropertiesSizeNeverDecreasesUpTheSuperChain()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var super = layout.OffsetOrThrow(LayoutNames.SuperStruct);
        var size = layout.OffsetOrThrow(LayoutNames.PropertiesSize);

        var current = graph.ClassNamed("Class");

        Assert.True(graph.Memory.TryRead(current + size, out int child));

        while (graph.Memory.TryRead(current + super, out nint parent) && parent is not 0)
        {
            Assert.True(graph.Memory.TryRead(parent + size, out int value));
            Assert.True(value <= child);

            child = value;
            current = parent;
        }
    }

    [Fact]
    public void WithoutTheObjectBaseLayoutNothingIsAttempted()
    {
        var graph = FakeObjectGraph.Build();
        var gate = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, gate, out var view));

        var broken = new LayoutTable(
        [
            LayoutMember.Undetermined(LayoutNames.ClassPrivate, 0, "none"),
            LayoutMember.Undetermined(LayoutNames.NamePrivate, 0, "none"),
            LayoutMember.Undetermined(LayoutNames.OuterPrivate, 0, "none")
        ]);

        Assert.Throws<InvalidOperationException>(() => new StructProbe(graph.Memory, view, graph.Names, broken));
    }

    private static LayoutTable Probe(FakeObjectGraph graph)
    {
        var gate = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, gate, out var view));

        var header = new ObjectBaseProbe(graph.Memory, view, graph.Names).Probe();

        Assert.True(header.IsComplete, string.Join(Environment.NewLine, header.Members));

        return new StructProbe(graph.Memory, view, graph.Names, header).Probe();
    }
}