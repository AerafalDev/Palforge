using Palforge.Layout;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Unreal.Probes;

public sealed class FunctionProbeTests
{
    [Fact]
    public void TheFunctionProbeDerivesEveryMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));
        Assert.True(layout.IsFullyVerified);

        Assert.Equal(graph.Layout.FunctionNumParms, layout.OffsetOrThrow(LayoutNames.NumParms));
        Assert.Equal(graph.Layout.FunctionParmsSize, layout.OffsetOrThrow(LayoutNames.ParmsSize));
        Assert.Equal(graph.Layout.EnumNames, layout.OffsetOrThrow(LayoutNames.EnumNames));
    }

    [Fact]
    public void TheFunctionProbeFollowsWhereverTheLayoutMoves()
    {
        var shifted = new FakeLayout { FunctionNumParms = 0xD0, FunctionParmsSize = 0xD8, EnumNames = 0x68 };
        var layout = Probe(FakeObjectGraph.Build(shifted));

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(shifted.FunctionNumParms, layout.OffsetOrThrow(LayoutNames.NumParms));
        Assert.Equal(shifted.FunctionParmsSize, layout.OffsetOrThrow(LayoutNames.ParmsSize));
        Assert.Equal(shifted.EnumNames, layout.OffsetOrThrow(LayoutNames.EnumNames));
    }

    [Fact]
    public void ParameterCountsComeFromTheChildPropertyChain()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var offset = layout.OffsetOrThrow(LayoutNames.NumParms);

        Assert.True(graph.Memory.TryRead(graph.FunctionNamed("Class.CreateDefaultObject") + offset, out int three));
        Assert.True(graph.Memory.TryRead(graph.FunctionNamed("Class.PurgeClass") + offset, out int none));

        Assert.Equal(3, three);
        Assert.Equal(0, none);
    }

    [Fact]
    public void EveryEnumMemberIsNamedAfterItsEnum()
    {
        var graph = FakeObjectGraph.Build();
        var layout = Probe(graph);

        var offset = layout.OffsetOrThrow(LayoutNames.EnumNames);
        var enumeration = graph.ClassNamed("ENetRole");

        Assert.True(graph.Memory.TryRead(enumeration + offset, out nint pairs));
        Assert.True(graph.Memory.TryRead(enumeration + offset + nint.Size, out int count));
        Assert.Equal(4, count);

        for (var index = 0; index < count; index++)
        {
            Assert.True(graph.Memory.TryRead(pairs + index * 0x10, out int id));
            Assert.True(graph.Names.TryResolve(id, out var member));
            Assert.StartsWith("ENetRole::", member, StringComparison.Ordinal);
        }
    }

    private static LayoutTable Probe(FakeObjectGraph graph)
    {
        var gate = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, gate, out var view));

        var objectBase = new ObjectBaseProbe(graph.Memory, view, graph.Names).Probe();
        var structs = new StructProbe(graph.Memory, view, graph.Names, objectBase).Probe();

        var known = new LayoutBuilder().AddDerived(objectBase).AddDerived(structs).Build();
        var properties = new PropertyProbe(graph.Memory, view, graph.Names, known).Probe();

        var full = new LayoutBuilder().AddDerived(known).AddDerived(properties).Build();

        return new FunctionProbe(graph.Memory, view, graph.Names, full).Probe();
    }
}