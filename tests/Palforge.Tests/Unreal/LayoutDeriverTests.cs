using Palforge.Tests.Memory;
using Palforge.Tests.Unreal.Probes;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Stage;

namespace Palforge.Tests.Unreal;

public sealed class LayoutDeriverTests
{
    [Fact]
    public void TheDeriverProducesAReadyLayoutFromTheWholeGraph()
    {
        var graph = FakeObjectGraph.Build();
        var layout = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names);

        Assert.True(layout.IsReady, $"{layout.FailedAt}: {layout.FailureReason}");
        Assert.True(layout.Layout.IsComplete);
    }

    [Fact]
    public void EveryDerivableMemberKeepsItsDerivedProvenance()
    {
        var graph = FakeObjectGraph.Build();
        var layout = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names).Layout;

        Assert.Equal(graph.Layout.ClassPrivate, layout.OffsetOrThrow(LayoutNames.ClassPrivate));
        Assert.Equal(graph.Layout.SuperStruct, layout.OffsetOrThrow(LayoutNames.SuperStruct));
        Assert.Equal(graph.Layout.PropertyBaseSize, layout.OffsetOrThrow(LayoutNames.PropertyBaseSize));

        Assert.Equal(Palforge.Layout.Provenance.Derived, layout[LayoutNames.SuperStruct].Provenance);
    }

    [Fact]
    public void TheVersionTableFillsTheNonDerivableMembers()
    {
        var graph = FakeObjectGraph.Build();
        var layout = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names).Layout;

        Assert.Equal(UnrealVersionTable.Offsets[LayoutNames.ProcessEventSlot], layout.OffsetOrThrow(LayoutNames.ProcessEventSlot));
        Assert.Equal(Palforge.Layout.Provenance.Tabled, layout[LayoutNames.ProcessEventSlot].Provenance);
    }

    [Fact]
    public void AGraphWithNoObjectArrayFailsAtTheGate()
    {
        var memory = new FakeMemory();
        var layout = LayoutDeriver.Derive(memory, memory.Allocate(0x200), new FakeNameTable());

        Assert.False(layout.IsReady);
        Assert.Equal(DerivationStage.ObjectArray, layout.FailedAt);
    }

    [Fact]
    public void AFailedLayoutRefusesToHandOutItsTable()
    {
        var memory = new FakeMemory();
        var layout = LayoutDeriver.Derive(memory, memory.Allocate(0x200), new FakeNameTable());

        Assert.Throws<InvalidOperationException>(() => layout.Layout);
        Assert.Throws<InvalidOperationException>(() => layout.Objects);
        Assert.Throws<InvalidOperationException>(() => layout.Names);
    }

    [Fact]
    public void ACorruptedGraphFailsHardRatherThanShippingAWrongRuntime()
    {
        var graph = FakeObjectGraph.Build();

        Assert.True(graph.Memory.TryWrite(graph.ClassNamed("Class") + graph.Layout.SuperStruct, (nint)0));

        var layout = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names);

        Assert.False(layout.IsReady);
        Assert.Throws<InvalidOperationException>(() => layout.Layout);
    }

    [Fact]
    public void TheForkDetectorFlagsAGraphThatDivergesFromStockUnreal()
    {
        var graph = FakeObjectGraph.Build();
        var layout = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names);

        Assert.True(layout.IsReady, $"{layout.FailedAt}: {layout.FailureReason}");
        Assert.NotEmpty(layout.Conflicts);
        Assert.Contains(layout.Conflicts, static conflict => conflict.Contains(LayoutNames.SuperStruct, StringComparison.Ordinal));
    }

    [Fact]
    public void TheFingerprintIsStableAndChangesWithTheLayout()
    {
        var standard = DeriveFingerprint(new FakeLayout());
        var again = DeriveFingerprint(new FakeLayout());
        var moved = DeriveFingerprint(new FakeLayout { SuperStruct = 0x58, Children = 0x68, ChildProperties = 0x78, PropertiesSize = 0x88 });

        Assert.Equal(standard, again);
        Assert.NotEqual(standard, moved);
    }

    private static string DeriveFingerprint(FakeLayout layout)
    {
        var graph = FakeObjectGraph.Build(layout);
        var derived = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names);

        Assert.True(derived.IsReady, $"{derived.FailedAt}: {derived.FailureReason}");

        return derived.Fingerprint;
    }
}