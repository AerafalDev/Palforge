using Palforge.Tests.Unreal.Probes;
using Palforge.Unreal.Reflection;
using Palforge.Unreal.Stage;

namespace Palforge.Tests.Unreal.Reflection;

public sealed class UnrealContextTests
{
    private static UnrealContext Build(out FakeObjectGraph graph)
    {
        graph = FakeObjectGraph.Build();

        var derived = LayoutDeriver.Derive(graph.Memory, graph.ObjectArray, graph.Names);

        Assert.True(derived.IsReady, $"{derived.FailedAt}: {derived.FailureReason}");

        return new UnrealContext(graph.Memory, derived.Layout, derived.Names, derived.Objects, gmalloc: 0);
    }

    [Fact]
    public void FindClassReturnsTheNamedClassObject()
    {
        var context = Build(out _);

        var klass = context.FindClass("Class");

        Assert.NotNull(klass);
        Assert.Equal("Class", klass.Name);
    }

    [Fact]
    public void AClassClimbsItsSuperChain()
    {
        var context = Build(out _);

        var klass = context.FindClass("Class");

        Assert.Equal("Struct", klass!.SuperClass?.Name);
        Assert.Equal("Field", klass.SuperClass?.SuperClass?.Name);
        Assert.Equal("Object", klass.SuperClass?.SuperClass?.SuperClass?.Name);
        Assert.Null(klass.SuperClass?.SuperClass?.SuperClass?.SuperClass);
    }

    [Fact]
    public void WrapClassifiesByWhatTheClassDerivesFrom()
    {
        var context = Build(out var graph);

        Assert.IsType<UClass>(context.Wrap(graph.ClassNamed("Class")));
        Assert.IsType<UFunction>(context.Wrap(graph.FunctionNamed("Object.ExecuteUbergraph")));
        Assert.IsType<UScriptStruct>(context.Wrap(graph.ClassNamed("Vector")));

        Assert.Equal(typeof(UObject), context.FindObject("Filler_0")!.GetType());
    }

    [Fact]
    public void AnObjectKnowsItsClassAndOuter()
    {
        var context = Build(out var graph);

        var filler = context.FindObject("Filler_0");

        Assert.NotNull(filler);
        Assert.Equal("Object", filler.Class?.Name);
        Assert.Equal(graph.Package, filler.Outer?.Address);
    }

    [Fact]
    public void IsAWalksTheClassHierarchy()
    {
        var context = Build(out _);

        var filler = context.FindObject("Filler_0")!;
        var objectClass = context.FindClass("Object")!;
        var classClass = context.FindClass("Class")!;

        Assert.True(filler.IsA(objectClass));
        Assert.False(filler.IsA(classClass));
    }

    [Fact]
    public void AClassExposesItsDefaultObject()
    {
        var context = Build(out _);

        var cdo = context.FindClass("Class")!.ClassDefaultObject;

        Assert.Equal("Default__Class", cdo?.Name);
    }

    [Fact]
    public void AStructEnumeratesItsPropertiesWithKindOffsetAndSize()
    {
        var context = Build(out var graph);

        var vector = context.AsStruct(graph.ClassNamed("Vector"))!;
        var properties = vector.Properties.ToArray();

        Assert.Equal(["X", "Y", "Z"], properties.Select(static p => p.Name));
        Assert.All(properties, static p => Assert.Equal(PropertyKind.Double, p.Kind));
        Assert.All(properties, static p => Assert.Equal(8, p.ElementSize));
        Assert.All(properties, static p => Assert.Equal(1, p.ArrayDim));
        Assert.Equal([0, 8, 16], properties.Select(static p => p.Offset));
    }

    [Fact]
    public void GetAndSetReadWriteTheValueAndVerifyElementSize()
    {
        var context = Build(out var graph);

        var y = context.AsStruct(graph.ClassNamed("Vector"))!.FindProperty("Y")!;
        var container = graph.Memory.Allocate(0x20);

        y.SetAt(container, 3.5);
        Assert.Equal(3.5, y.GetAt<double>(container));

        Assert.Throws<InvalidOperationException>(() => y.GetAt<int>(container));
        Assert.Throws<InvalidOperationException>(() => y.SetAt(container, 1));
    }

    [Fact]
    public void FindPropertyClimbsTheSuperChain()
    {
        var context = Build(out var graph);

        var transform = context.AsStruct(graph.ClassNamed("Transform"))!;

        Assert.Equal(PropertyKind.Struct, transform.FindProperty("Translation")!.Kind);
        Assert.Equal(PropertyKind.Object, transform.FindProperty("Holder")!.Kind);
        Assert.Null(transform.FindProperty("DoesNotExist"));
    }

    [Fact]
    public void AnObjectPropertyDereferencesGuardedAndWraps()
    {
        var context = Build(out var graph);

        var holder = context.AsStruct(graph.ClassNamed("Transform"))!.FindProperty("Holder")!;
        var typed = Assert.IsType<FObjectProperty>(holder);
        var container = graph.Memory.Allocate(0x40);

        graph.Memory.TryWrite(container + holder.Offset, graph.ClassNamed("Class"));
        Assert.Equal("Class", typed.GetObjectAt(container)?.Name);

        graph.Memory.TryWrite(container + holder.Offset, (nint)0);
        Assert.Null(typed.GetObjectAt(container));

        typed.SetObjectAt(container, context.Wrap(graph.ClassNamed("Object")));
        Assert.Equal("Object", typed.GetObjectAt(container)?.Name);
    }

    [Fact]
    public void ABoolPropertyMasksTheBitfield()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var address = BuildProperty(graph, "BoolProperty", EClassCastFlags.BoolProperty, 1);
        graph.Memory.TryWrite<byte>(address + fake.PropertyBaseSize + 1, 0);
        graph.Memory.TryWrite<byte>(address + fake.PropertyBaseSize + 3, 0x04);

        var boolean = Assert.IsType<FBoolProperty>(context.WrapProperty(address));

        Assert.Equal((byte)0x04, boolean.FieldMask);
        Assert.False(boolean.IsNativeBool);

        var container = graph.Memory.Allocate(0x10);

        graph.Memory.TryWrite<byte>(container, 0x04);
        Assert.True(boolean.GetBoolAt(container));

        graph.Memory.TryWrite<byte>(container, 0x00);
        Assert.False(boolean.GetBoolAt(container));

        boolean.SetBoolAt(container, true);
        Assert.True(graph.Memory.TryRead(container, out byte raw));
        Assert.Equal((byte)0x04, raw);
    }

    [Fact]
    public void ANamePropertyResolvesTheName()
    {
        var context = Build(out var graph);

        var address = BuildProperty(graph, "NameProperty", EClassCastFlags.NameProperty, 8);
        var name = Assert.IsType<FNameProperty>(context.WrapProperty(address));
        var container = graph.Memory.Allocate(0x10);

        graph.Memory.TryWrite(container, graph.Names.Intern("Pal_Sheepball"));
        Assert.Equal("Pal_Sheepball", name.GetNameAt(container));
    }

    [Fact]
    public void AStringPropertyReadsTheFString()
    {
        var context = Build(out var graph);

        var address = BuildProperty(graph, "StrProperty", EClassCastFlags.StrProperty, 16);
        var text = Assert.IsType<FStrProperty>(context.WrapProperty(address));
        var container = graph.Memory.Allocate(0x20);

        const string value = "Palforge";
        var buffer = graph.Memory.Allocate((value.Length + 1) * sizeof(char));

        for (var index = 0; index < value.Length; index++)
            graph.Memory.TryWrite(buffer + index * sizeof(char), value[index]);

        graph.Memory.TryWrite(buffer + value.Length * sizeof(char), '\0');
        graph.Memory.TryWrite(container, buffer);
        graph.Memory.TryWrite(container + nint.Size, value.Length + 1);
        graph.Memory.TryWrite(container + nint.Size + sizeof(int), value.Length + 1);

        Assert.Equal("Palforge", text.GetStringAt(container));

        Assert.False(text.SetStringAt(container, "changed"));
        Assert.Equal("Palforge", text.GetStringAt(container));
    }

    [Fact]
    public void FindFirstOfReturnsALiveInstanceNotTheDefaultObject()
    {
        var context = Build(out _);

        var instance = context.FindFirstOf("Object");

        Assert.NotNull(instance);
        Assert.False(instance.IsDefaultObject);
        Assert.True(instance.IsA(context.FindClass("Object")!));
    }

    [Fact]
    public void FormatValueRendersEachKind()
    {
        var context = Build(out var graph);
        var container = graph.Memory.Allocate(0x20);

        var integer = context.WrapProperty(BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4))!;
        graph.Memory.TryWrite(container, 42);
        Assert.Equal("42", integer.FormatValue(container));

        var name = context.WrapProperty(BuildProperty(graph, "NameProperty", EClassCastFlags.NameProperty, 8, 0x08))!;
        graph.Memory.TryWrite(container + 0x08, graph.Names.Intern("Pal_Sheepball"));
        Assert.Equal("Pal_Sheepball", name.FormatValue(container));
    }

    [Fact]
    public void AnEnumResolvesNamesAndValuesBothWays()
    {
        var context = Build(out var graph);

        var enumeration = Assert.IsType<UEnum>(context.Wrap(graph.ClassNamed("EEngineMode")));

        Assert.Equal("EEngineMode::Game", enumeration.GetNameByValue(1));
        Assert.Equal(2, enumeration.GetValueByName("EEngineMode::Server"));
        Assert.Null(enumeration.GetNameByValue(99));
    }

    [Fact]
    public void AnEnumPropertyRendersTheMemberName()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var address = BuildProperty(graph, "EnumProperty", EClassCastFlags.EnumProperty, 1);
        graph.Memory.TryWrite(address + fake.PropertyBaseSize + nint.Size, graph.ClassNamed("EEngineMode"));

        var enumProperty = Assert.IsType<FEnumProperty>(context.WrapProperty(address));
        var container = graph.Memory.Allocate(0x10);

        graph.Memory.TryWrite<byte>(container, 1);
        Assert.Equal("EEngineMode::Game", enumProperty.FormatValue(container));

        graph.Memory.TryWrite<byte>(container, 7);
        Assert.Equal("7", enumProperty.FormatValue(container));

        enumProperty.SetValueAt(container, 2);
        Assert.Equal("EEngineMode::Server", enumProperty.FormatValue(container));
    }

    [Fact]
    public void AStructPropertyRecursesIntoItsMembers()
    {
        var context = Build(out var graph);

        var translation = context.AsStruct(graph.ClassNamed("Transform"))!.FindProperty("Translation")!;
        var typed = Assert.IsType<FStructProperty>(translation);

        Assert.Equal("Vector", typed.Struct?.Name);

        var container = graph.Memory.Allocate(0x40);
        graph.Memory.TryWrite(container + 0, 1.0);
        graph.Memory.TryWrite(container + 8, 2.0);
        graph.Memory.TryWrite(container + 16, 3.0);

        Assert.Equal("{X=1, Y=2, Z=3}", typed.FormatValue(container));
    }

    [Fact]
    public void AStructPropertyWritesTheWholeValueAsRawBytes()
    {
        var context = Build(out var graph);
        var typed = Assert.IsType<FStructProperty>(context.AsStruct(graph.ClassNamed("Transform"))!.FindProperty("Translation")!);

        var container = graph.Memory.Allocate(0x40);
        graph.Memory.TryWrite(container + typed.Offset + 0, 1.0);
        graph.Memory.TryWrite(container + typed.Offset + 8, 2.0);
        graph.Memory.TryWrite(container + typed.Offset + 16, 3.0);

        var original = typed.GetValueAt(container);
        Assert.Equal(typed.ElementSize, original.Length);

        var modified = (byte[])original.Clone();
        BitConverter.GetBytes(9.0).CopyTo(modified, 0);

        Assert.True(typed.SetValueAt(container, modified));
        Assert.Equal("{X=9, Y=2, Z=3}", typed.FormatValue(container));

        Assert.False(typed.SetValueAt(container, new byte[typed.ElementSize - 1]));
        Assert.Equal("{X=9, Y=2, Z=3}", typed.FormatValue(container));

        Assert.True(typed.SetValueAt(container, original));
        Assert.Equal("{X=1, Y=2, Z=3}", typed.FormatValue(container));
    }

    [Fact]
    public void AnArrayPropertyRendersItsElements()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var inner = BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4);
        var array = BuildProperty(graph, "ArrayProperty", EClassCastFlags.ArrayProperty, 16);
        graph.Memory.TryWrite(array + fake.PropertyBaseSize, inner);

        var data = graph.Memory.Allocate(3 * sizeof(int));
        graph.Memory.TryWrite(data + 0, 10);
        graph.Memory.TryWrite(data + 4, 20);
        graph.Memory.TryWrite(data + 8, 30);

        var container = graph.Memory.Allocate(0x20);
        graph.Memory.TryWrite(container, data);
        graph.Memory.TryWrite(container + nint.Size, 3);
        graph.Memory.TryWrite(container + nint.Size + sizeof(int), 3);

        var typed = Assert.IsType<FArrayProperty>(context.WrapProperty(array));

        Assert.Equal(PropertyKind.Int32, typed.Inner?.Kind);
        Assert.Equal(3, typed.CountAt(container));
        Assert.Equal("[10, 20, 30]", typed.FormatValue(container));
    }

    [Fact]
    public void AnArrayInsertsRemovesAndClearsInPlaceWithSpareCapacity()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var inner = BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4);
        var array = BuildProperty(graph, "ArrayProperty", EClassCastFlags.ArrayProperty, 16);
        graph.Memory.TryWrite(array + fake.PropertyBaseSize, inner);

        var data = graph.Memory.Allocate(8 * sizeof(int));
        graph.Memory.TryWrite(data + 0, 10);
        graph.Memory.TryWrite(data + 4, 20);
        graph.Memory.TryWrite(data + 8, 30);

        var container = graph.Memory.Allocate(0x20);
        graph.Memory.TryWrite(container, data);
        graph.Memory.TryWrite(container + nint.Size, 3);
        graph.Memory.TryWrite(container + nint.Size + sizeof(int), 8);

        var typed = Assert.IsType<FArrayProperty>(context.WrapProperty(array));

        Assert.Equal(3, typed.AddAt(container, BitConverter.GetBytes(40)));
        Assert.Equal("[10, 20, 30, 40]", typed.FormatValue(container));

        Assert.Equal(1, typed.InsertAt(container, 1, BitConverter.GetBytes(15)));
        Assert.Equal("[10, 15, 20, 30, 40]", typed.FormatValue(container));

        Assert.Equal(-1, typed.AddAt(container, new byte[3]));
        Assert.Equal(5, typed.CountAt(container));

        Assert.True(typed.RemoveAt(container, 1));
        Assert.True(typed.RemoveAt(container, 3));
        Assert.Equal("[10, 20, 30]", typed.FormatValue(container));

        Assert.True(typed.SetElementAt(container, 0, BitConverter.GetBytes(99)));
        Assert.Equal("[99, 20, 30]", typed.FormatValue(container));

        Assert.True(typed.ClearAt(container));
        Assert.Equal(0, typed.CountAt(container));
    }

    [Fact]
    public void AMapPropertyReadsPairsSkippingHoles()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var key = BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4);
        var value = BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4);
        var map = BuildProperty(graph, "MapProperty", EClassCastFlags.MapProperty, 0x50);

        graph.Memory.TryWrite(map + fake.PropertyBaseSize, key);
        graph.Memory.TryWrite(map + fake.PropertyBaseSize + nint.Size, value);
        graph.Memory.TryWrite(map + fake.PropertyBaseSize + 2 * nint.Size, 8);
        graph.Memory.TryWrite(map + fake.PropertyBaseSize + 2 * nint.Size + 0x14, 16);

        var pairs = graph.Memory.Allocate(3 * 16);
        graph.Memory.TryWrite(pairs + 0, 1);
        graph.Memory.TryWrite(pairs + 8, 100);
        graph.Memory.TryWrite(pairs + 32, 3);
        graph.Memory.TryWrite(pairs + 40, 300);

        var container = graph.Memory.Allocate(0x50);
        graph.Memory.TryWrite(container + 0x00, pairs);
        graph.Memory.TryWrite(container + 0x08, 3);
        graph.Memory.TryWrite<uint>(container + 0x10, 0b101);
        graph.Memory.TryWrite(container + 0x20, (nint)0);
        graph.Memory.TryWrite(container + 0x34, 1);

        var typed = Assert.IsType<FMapProperty>(context.WrapProperty(map));

        Assert.Equal(2, typed.CountAt(container));
        Assert.Equal("{1: 100, 3: 300}", typed.FormatValue(container));
    }

    [Fact]
    public void ASetPropertyReadsElementsSkippingHoles()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var element = BuildProperty(graph, "IntProperty", EClassCastFlags.IntProperty, 4);
        var set = BuildProperty(graph, "SetProperty", EClassCastFlags.SetProperty, 0x50);

        graph.Memory.TryWrite(set + fake.PropertyBaseSize, element);
        graph.Memory.TryWrite(set + fake.PropertyBaseSize + nint.Size + 0x10, 8);

        var slots = graph.Memory.Allocate(3 * 8);
        graph.Memory.TryWrite(slots + 0, 10);
        graph.Memory.TryWrite(slots + 16, 30);

        var container = graph.Memory.Allocate(0x50);
        graph.Memory.TryWrite(container + 0x00, slots);
        graph.Memory.TryWrite(container + 0x08, 3);
        graph.Memory.TryWrite<uint>(container + 0x10, 0b101);
        graph.Memory.TryWrite(container + 0x20, (nint)0);
        graph.Memory.TryWrite(container + 0x34, 1);

        var typed = Assert.IsType<FSetProperty>(context.WrapProperty(set));

        Assert.Equal(2, typed.CountAt(container));
        Assert.Equal("{10, 30}", typed.FormatValue(container));
    }

    [Fact]
    public void ASoftObjectPropertyReadsItsAssetPath()
    {
        var context = Build(out var graph);

        var soft = BuildProperty(graph, "SoftObjectProperty", EClassCastFlags.SoftObjectProperty, 0x30);
        var typed = Assert.IsType<FSoftObjectProperty>(context.WrapProperty(soft));
        var container = graph.Memory.Allocate(0x40);

        graph.Memory.TryWrite(container + 0x10, graph.Names.Intern("/Game/Pal/Blueprint/Character"));
        graph.Memory.TryWrite(container + 0x18, graph.Names.Intern("BP_SheepBall"));

        Assert.Equal("/Game/Pal/Blueprint/Character.BP_SheepBall", typed.GetPathAt(container));
    }

    [Fact]
    public void AWeakObjectPropertyResolvesAndRejectsStaleSerials()
    {
        var context = Build(out var graph);

        var weak = BuildProperty(graph, "WeakObjectProperty", EClassCastFlags.WeakObjectProperty, 8);
        var typed = Assert.IsType<FWeakObjectProperty>(context.WrapProperty(weak));
        var container = graph.Memory.Allocate(0x10);

        graph.Memory.TryWrite(container + 0, 0);
        graph.Memory.TryWrite(container + 4, 1);
        Assert.NotNull(typed.GetObjectAt(container));

        graph.Memory.TryWrite(container + 4, 999);
        Assert.Null(typed.GetObjectAt(container));

        graph.Memory.TryWrite(container + 4, 0);
        Assert.Null(typed.GetObjectAt(container));
    }

    [Fact]
    public void ABytePropertyRendersEnumMemberOrNumber()
    {
        var context = Build(out var graph);
        var fake = graph.Layout;

        var enumByte = BuildProperty(graph, "ByteProperty", EClassCastFlags.ByteProperty, 1);
        graph.Memory.TryWrite(enumByte + fake.PropertyBaseSize, graph.ClassNamed("ENetRole"));

        var plainByte = BuildProperty(graph, "ByteProperty", EClassCastFlags.ByteProperty, 1, 1);

        var container = graph.Memory.Allocate(0x10);
        graph.Memory.TryWrite<byte>(container + 0, 2);
        graph.Memory.TryWrite<byte>(container + 1, 42);

        Assert.Equal("ENetRole::AutonomousProxy", Assert.IsType<FByteProperty>(context.WrapProperty(enumByte)).FormatValue(container));
        Assert.Equal("42", Assert.IsType<FByteProperty>(context.WrapProperty(plainByte)).FormatValue(container));
    }

    [Fact]
    public void AnInterfacePropertyReadsItsObject()
    {
        var context = Build(out var graph);

        var iface = BuildProperty(graph, "InterfaceProperty", EClassCastFlags.InterfaceProperty, 16);
        var typed = Assert.IsType<FInterfaceProperty>(context.WrapProperty(iface));
        var container = graph.Memory.Allocate(0x20);

        graph.Memory.TryWrite(container, graph.ClassNamed("Class"));
        Assert.Equal("Class", typed.GetObjectAt(container)?.Name);
    }

    [Fact]
    public void ADelegatePropertyReadsBoundObjectAndFunction()
    {
        var context = Build(out var graph);

        var del = BuildProperty(graph, "DelegateProperty", EClassCastFlags.DelegateProperty, 16);
        var typed = Assert.IsType<FDelegateProperty>(context.WrapProperty(del));
        var container = graph.Memory.Allocate(0x20);

        graph.Memory.TryWrite(container + 0, 0);
        graph.Memory.TryWrite(container + 4, 1);
        graph.Memory.TryWrite(container + 8, graph.Names.Intern("OnSomethingHappened"));

        Assert.EndsWith(".OnSomethingHappened", typed.FormatValue(container));

        graph.Memory.TryWrite(container + 4, 0);
        Assert.Equal("None", typed.FormatValue(container));
    }

    [Fact]
    public void AMulticastDelegatePropertyRendersItsInvocationList()
    {
        var context = Build(out var graph);

        var multicast = BuildProperty(graph, "MulticastInlineDelegateProperty", EClassCastFlags.MulticastInlineDelegateProperty, 16);
        var typed = Assert.IsType<FMulticastInlineDelegateProperty>(context.WrapProperty(multicast));

        var bindings = graph.Memory.Allocate(2 * 16);
        graph.Memory.TryWrite(bindings + 0, 0);
        graph.Memory.TryWrite(bindings + 4, 1);
        graph.Memory.TryWrite(bindings + 8, graph.Names.Intern("OnFirst"));
        graph.Memory.TryWrite(bindings + 16, 0);
        graph.Memory.TryWrite(bindings + 20, 1);
        graph.Memory.TryWrite(bindings + 24, graph.Names.Intern("OnSecond"));

        var container = graph.Memory.Allocate(0x20);
        graph.Memory.TryWrite(container + 0, bindings);
        graph.Memory.TryWrite(container + nint.Size, 2);
        graph.Memory.TryWrite(container + nint.Size + sizeof(int), 2);

        var rendered = typed.FormatValue(container);
        Assert.Contains(".OnFirst", rendered);
        Assert.Contains(".OnSecond", rendered);
    }

    [Fact]
    public void ALazyObjectPropertyRendersItsGuid()
    {
        var context = Build(out var graph);

        var lazy = BuildProperty(graph, "LazyObjectProperty", EClassCastFlags.LazyObjectProperty, 0x1C);
        var typed = Assert.IsType<FLazyObjectProperty>(context.WrapProperty(lazy));
        var container = graph.Memory.Allocate(0x30);

        Assert.Equal("None", typed.FormatValue(container));

        graph.Memory.TryWrite(container + 0x10, 0xDEADBEEF);
        graph.Memory.TryWrite<uint>(container + 0x14, 0x01020304);
        Assert.Equal("DEADBEEF01020304" + "0000000000000000", typed.FormatValue(container));
    }

    [Fact]
    public void ATextPropertyGuardsANonModuleVtableInsteadOfCallingIt()
    {
        var context = Build(out var graph);

        var text = BuildProperty(graph, "TextProperty", EClassCastFlags.TextProperty, 16);
        var typed = Assert.IsType<FTextProperty>(context.WrapProperty(text));
        var container = graph.Memory.Allocate(0x20);
        var textData = graph.Memory.Allocate(0x10);
        graph.Memory.TryWrite(container, textData);
        graph.Memory.TryWrite(textData, graph.Memory.Allocate(0x40));

        Assert.Equal(string.Empty, typed.GetTextAt(container));
    }

    [Fact]
    public void ASparseDelegatePropertyReportsBoundState()
    {
        var context = Build(out var graph);

        var sparse = BuildProperty(graph, "MulticastSparseDelegateProperty", EClassCastFlags.MulticastSparseDelegateProperty, 1);
        var typed = Assert.IsType<FMulticastSparseDelegateProperty>(context.WrapProperty(sparse));
        var container = graph.Memory.Allocate(0x10);

        graph.Memory.TryWrite<byte>(container, 1);
        Assert.True(typed.IsBoundAt(container));

        graph.Memory.TryWrite<byte>(container, 0);
        Assert.False(typed.IsBoundAt(container));
    }

    private static nint BuildProperty(FakeObjectGraph graph, string className, EClassCastFlags castFlags, int elementSize, int offset = 0, EPropertyFlags propertyFlags = EPropertyFlags.IsPlainOldData | EPropertyFlags.NoDestructor | EPropertyFlags.ZeroConstructor)
    {
        var fake = graph.Layout;

        var fieldClass = graph.Memory.Allocate(fake.FieldClassSize);
        graph.Memory.TryWrite(fieldClass + fake.FieldClassName, graph.Names.Intern(className));
        graph.Memory.TryWrite(fieldClass + fake.FieldClassCastFlags, (ulong)castFlags);

        var address = graph.Memory.Allocate(fake.PropertyBaseSize + 0x40);
        graph.Memory.TryWrite(address + fake.FFieldClassPrivate, fieldClass);
        graph.Memory.TryWrite(address + fake.FFieldNamePrivate, graph.Names.Intern("prop_" + className));
        graph.Memory.TryWrite(address + fake.ArrayDim, 1);
        graph.Memory.TryWrite(address + fake.ElementSize, elementSize);
        graph.Memory.TryWrite(address + fake.OffsetInternal, offset);
        graph.Memory.TryWrite(address + fake.PropertyFlags, (ulong)propertyFlags);

        return address;
    }
}