using Palforge.Tests.Memory;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Unreal.Probes;

public sealed class ObjectArrayProbeTests
{
    [Fact]
    public void TheGateDerivesEveryArrayMember()
    {
        var graph = FakeObjectGraph.Build();
        var layout = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));
        Assert.True(layout.IsFullyVerified);

        Assert.Equal(graph.Layout.ObjObjects + graph.Layout.ChunkedObjects, layout.OffsetOrThrow(LayoutNames.ObjectsTable));
        Assert.Equal(graph.Layout.ItemStride, layout.OffsetOrThrow(LayoutNames.ItemStride));
        Assert.Equal(graph.Layout.ItemObject, layout.OffsetOrThrow(LayoutNames.ItemObject));
        Assert.Equal(graph.Layout.ElementsPerChunk, layout.OffsetOrThrow(LayoutNames.ElementsPerChunk));
        Assert.Equal(graph.Layout.InternalIndex, layout.OffsetOrThrow(LayoutNames.InternalIndex));
        Assert.Equal(graph.Layout.ObjObjects + graph.Layout.ChunkedNumElements, layout.OffsetOrThrow(LayoutNames.NumElements));
    }

    [Theory]
    [InlineData(0x10, 0x18, 0x28, 32)]
    [InlineData(0x28, 0x38, 0x18, 128)]
    [InlineData(0x08, 0x0C, 0x30, 16)]
    public void TheGateFollowsWhereverTheLayoutMoves(int objObjects, int internalIndex, int itemStride, int elementsPerChunk)
    {
        var shifted = new FakeLayout
        {
            ObjObjects = objObjects,
            InternalIndex = internalIndex,
            ItemStride = itemStride,
            ElementsPerChunk = elementsPerChunk
        };

        var graph = FakeObjectGraph.Build(shifted, filler: elementsPerChunk * 3);
        var layout = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(objObjects, layout.OffsetOrThrow(LayoutNames.ObjectsTable));
        Assert.Equal(internalIndex, layout.OffsetOrThrow(LayoutNames.InternalIndex));
        Assert.Equal(itemStride, layout.OffsetOrThrow(LayoutNames.ItemStride));
        Assert.Equal(elementsPerChunk, layout.OffsetOrThrow(LayoutNames.ElementsPerChunk));
    }

    [Fact]
    public void TheGateDerivesTheChunkSizeFromASingleChunk()
    {
        var graph = FakeObjectGraph.Build(new FakeLayout { ElementsPerChunk = 4096, MaxChunks = 16 }, filler: 200);
        var layout = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(layout.IsComplete, string.Join(Environment.NewLine, layout.Members));

        Assert.Equal(4096, layout.OffsetOrThrow(LayoutNames.ElementsPerChunk));
        Assert.Equal(graph.Layout.ObjObjects + graph.Layout.ChunkedNumElements, layout.OffsetOrThrow(LayoutNames.NumElements));

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, layout, out var view));
        Assert.Equal(graph.Objects.Count, view.Count);
    }

    [Fact]
    public void TheGateFailsRatherThanGuessOnEmptyMemory()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(0x200);

        var layout = ObjectArrayProbe.Probe(memory, address);

        Assert.False(layout.IsComplete);
        Assert.Equal(6, layout.Declared);
        Assert.Equal(0, layout.Known);

        foreach (var member in layout.Members)
            Assert.False(member.TryGetOffset(out _));
    }

    [Fact]
    public void AFailedGateReportsTheWindowItSearched()
    {
        var memory = new FakeMemory();
        var layout = ObjectArrayProbe.Probe(memory, memory.Allocate(0x200));

        Assert.Contains(layout.Members, static member => member.Detail is not null && member.Detail.Contains("searched the array header", StringComparison.Ordinal));
    }

    [Fact]
    public void TheViewCannotBeBuiltFromAFailedGate()
    {
        var memory = new FakeMemory();
        var address = memory.Allocate(0x200);
        var layout = ObjectArrayProbe.Probe(memory, address);

        Assert.False(ObjectArrayView.TryCreate(memory, address, layout, out _));
    }

    [Fact]
    public void TheViewWalksEveryObjectAcrossChunkBoundaries()
    {
        var graph = FakeObjectGraph.Build();
        var layout = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, layout, out var view));
        Assert.Equal(graph.Objects.Count, view.Count);
        Assert.Equal(graph.Objects, [.. view.Addresses()]);
    }

    [Fact]
    public void EveryWalkedObjectStillStoresItsOwnIndex()
    {
        var graph = FakeObjectGraph.Build();
        var layout = ObjectArrayProbe.Probe(graph.Memory, graph.ObjectArray);

        Assert.True(ObjectArrayView.TryCreate(graph.Memory, graph.ObjectArray, layout, out var view));

        for (var index = 0; index < view.Count; index++)
        {
            Assert.True(view.TryGetAddress(index, out var address));
            Assert.True(graph.Memory.TryRead(address + view.InternalIndexOffset, out int stored));
            Assert.Equal(index, stored);
        }
    }
}