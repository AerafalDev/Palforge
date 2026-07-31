namespace Palforge.Tests.Unreal.Probes;

public sealed class FakeObjectGraphTests
{
    [Fact]
    public void TheGraphLayoutDiffersFromTheRealUnrealLayout()
    {
        var layout = new FakeLayout();

        Assert.NotEqual(0x0C, layout.InternalIndex);
        Assert.NotEqual(0x10, layout.ClassPrivate);
        Assert.NotEqual(0x18, layout.NamePrivate);
        Assert.NotEqual(0x40, layout.SuperStruct);
        Assert.NotEqual(0x70, layout.PropertyBaseSize);
        Assert.NotEqual(65536, layout.ElementsPerChunk);
    }

    [Fact]
    public void EveryObjectStoresItsOwnIndex()
    {
        var graph = FakeObjectGraph.Build();

        for (var index = 0; index < graph.Objects.Count; index++)
        {
            Assert.True(graph.Memory.TryRead(graph.Objects[index] + graph.Layout.InternalIndex, out int stored));
            Assert.Equal(index, stored);
        }
    }

    [Fact]
    public void ClassIsItsOwnClass()
    {
        var graph = FakeObjectGraph.Build();

        Assert.True(graph.Memory.TryRead(graph.ClassOfClass + graph.Layout.ClassPrivate, out nint klass));
        Assert.Equal(graph.ClassOfClass, klass);
    }

    [Fact]
    public void EveryClassDefaultObjectIsNamedAfterItsClass()
    {
        var graph = FakeObjectGraph.Build();

        foreach (var name in (string[])["Object", "Field", "Struct", "Class", "ScriptStruct"])
        {
            Assert.True(graph.Memory.TryRead(graph.ClassNamed(name) + graph.Layout.ClassDefaultObject, out nint cdo));
            Assert.True(graph.Memory.TryRead(cdo + graph.Layout.NamePrivate, out int id));
            Assert.True(graph.Names.TryResolve(id, out var resolved));
            Assert.Equal($"Default__{name}", resolved);
        }
    }

    [Fact]
    public void TheSuperChainClimbsToObject()
    {
        var graph = FakeObjectGraph.Build();

        Assert.True(graph.Memory.TryRead(graph.ClassNamed("Class") + graph.Layout.SuperStruct, out nint super));
        Assert.Equal(graph.ClassNamed("Struct"), super);

        Assert.True(graph.Memory.TryRead(graph.ClassNamed("Object") + graph.Layout.SuperStruct, out nint root));
        Assert.Equal(0, root);
    }

    [Fact]
    public void VectorCarriesThreeDoublesAtZeroEightSixteen()
    {
        var graph = FakeObjectGraph.Build();

        Assert.True(graph.Memory.TryRead(graph.ClassNamed("Vector") + graph.Layout.ChildProperties, out nint field));

        var offsets = new List<int>();
        var sizes = new List<int>();

        while (field is not 0)
        {
            Assert.True(graph.Memory.TryRead(field + graph.Layout.OffsetInternal, out int offset));
            Assert.True(graph.Memory.TryRead(field + graph.Layout.ElementSize, out int size));

            offsets.Add(offset);
            sizes.Add(size);

            Assert.True(graph.Memory.TryRead(field + graph.Layout.FFieldNext, out field));
        }

        Assert.Equal([0, 8, 16], offsets);
        Assert.Equal([8, 8, 8], sizes);
    }

    [Fact]
    public void TheObjectArrayResolvesEveryIndexBackToItsObject()
    {
        var graph = FakeObjectGraph.Build();
        var layout = graph.Layout;
        var chunked = graph.ObjectArray + layout.ObjObjects;

        Assert.True(graph.Memory.TryRead(chunked + layout.ChunkedNumElements, out int count));
        Assert.Equal(graph.Objects.Count, count);

        Assert.True(graph.Memory.TryRead(chunked + layout.ChunkedObjects, out nint table));

        for (var index = 0; index < count; index++)
        {
            var chunk = index / layout.ElementsPerChunk;
            var slot = index % layout.ElementsPerChunk;

            Assert.True(graph.Memory.TryRead(table + chunk * nint.Size, out nint items));
            Assert.True(graph.Memory.TryRead(items + slot * layout.ItemStride + layout.ItemObject, out nint address));

            Assert.Equal(graph.Objects[index], address);
        }
    }

    [Fact]
    public void TheGraphSpansMoreThanOneChunk()
    {
        var graph = FakeObjectGraph.Build();

        Assert.True(graph.Objects.Count > graph.Layout.ElementsPerChunk);
    }

    [Fact]
    public void AnOverlappingLayoutIsRejectedInsteadOfSilentlyClobbered()
    {
        var overlapping = new FakeLayout { InternalIndex = 0x30 };

        var error = Assert.Throws<ArgumentException>(() => FakeObjectGraph.Build(overlapping));

        Assert.Contains("overlaps", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALayoutThatOverflowsItsObjectIsRejected()
    {
        Assert.Throws<ArgumentException>(() => FakeObjectGraph.Build(new FakeLayout { OuterPrivate = 0x3C }));
    }

    [Fact]
    public void AShiftedLayoutProducesADifferentGraph()
    {
        var standard = FakeObjectGraph.Build();
        var shifted = FakeObjectGraph.Build(new FakeLayout { InternalIndex = 0x18, ClassPrivate = 0x08 });

        Assert.True(shifted.Memory.TryRead(shifted.Objects[7] + 0x18, out int index));
        Assert.Equal(7, index);

        Assert.True(standard.Memory.TryRead(standard.Objects[7] + 0x18, out int elsewhere));
        Assert.NotEqual(7, elsewhere);
    }
}