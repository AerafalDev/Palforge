using Palforge.Unreal.Reflection;
using Palforge.Unreal.Sdk;

namespace Palforge.Tests.Unreal.Sdk;

public sealed class TypeMapTests
{
    [Fact]
    public void AScalarMapsToBakedReadAndWrite()
    {
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Int32, 0x1F0, 4));

        Assert.Equal("int", accessor?.TypeName);
        Assert.Equal("ReadAt<int>(0x1F0)", accessor?.GetBody);
        Assert.Equal("WriteAt(0x1F0, value)", accessor?.SetBody);
    }

    [Fact]
    public void AByteEnumCastsThroughItsUnderlyingType()
    {
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Enum, 0x10, 1, "EMovementMode"));

        Assert.Equal("EMovementMode", accessor?.TypeName);
        Assert.Equal("(EMovementMode)ReadAt<byte>(0x10)", accessor?.GetBody);
        Assert.Equal("WriteAt(0x10, (byte)value)", accessor?.SetBody);
    }

    [Fact]
    public void ABitfieldBoolCarriesItsMask()
    {
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Bool, 0x8, 1, BoolMask: 0x2));

        Assert.Equal("bool", accessor?.TypeName);
        Assert.Equal("ReadBoolAt(0x8, 0x2)", accessor?.GetBody);
        Assert.Equal("WriteBoolAt(0x8, 0x2, value)", accessor?.SetBody);
    }

    [Fact]
    public void NameAndStringRoundTripButTextIsReadOnly()
    {
        Assert.Equal("WriteNameAt(0x20, value)", SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Name, 0x20, 8))?.SetBody);
        Assert.Equal("WriteStringAt(0x28, value)", SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Str, 0x28, 16))?.SetBody);
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Text, 0x38, 24))?.SetBody);
    }

    [Fact]
    public void AnObjectWrapsAndWritesThePointer()
    {
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Object, 0x30, 8));

        Assert.Equal("UObject?", accessor?.TypeName);
        Assert.Equal("WrapAt(0x30)", accessor?.GetBody);
        Assert.Equal("WriteObjectAt(0x30, value)", accessor?.SetBody);
    }

    [Fact]
    public void AStructMapsToALiveViewWithNoSetter()
    {
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Struct, 0x50, 24, "Vector"));

        Assert.Equal("Vector", accessor?.TypeName);
        Assert.Equal("new Vector(Address + 0x50, Context)", accessor?.GetBody);
        Assert.Null(accessor?.SetBody);
    }

    [Fact]
    public void TheSoftAndWeakReferenceKindsRoundTrip()
    {
        Assert.Equal(("string", "ReadSoftPathAt(0x40)", "WriteSoftPathAt(0x40, value)"), Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.SoftObject, 0x40, 40))));
        Assert.Equal(("UObject?", "ReadWeakAt(0x48)", "WriteWeakAt(0x48, value)"), Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.WeakObject, 0x48, 8))));
    }

    [Fact]
    public void TheLazyAndInterfaceKindsStayReadOnly()
    {
        Assert.Equal(("string", "ReadLazyGuidAt(0x50)", null), Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.LazyObject, 0x50, 28))));
        Assert.Equal(("UObject?", "WrapAt(0x58)", null), Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Interface, 0x58, 16))));
    }

    [Fact]
    public void DelegatePropertiesMapToTypedBindableViews()
    {
        Assert.Equal(("UnrealDelegate", "new UnrealDelegate(this, 0x60)", null), Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Delegate, 0x60, 16))));

        Assert.Equal(("UnrealMulticastDelegate", "new UnrealMulticastDelegate(this, 0x70, \"OnHit\", false)", null),
            Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.MulticastInlineDelegate, 0x70, 16, Name: "OnHit"))));
        Assert.Equal(("UnrealMulticastDelegate", "new UnrealMulticastDelegate(this, 0x80, \"OnOverlap\", true)", null),
            Tuple(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.MulticastSparseDelegate, 0x80, 16, Name: "OnOverlap"))));
    }

    [Fact]
    public void AScalarArrayMapsToATypedViewWithReaderAndWriter()
    {
        var element = new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4);
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x28, 16, Element: element, Name: "Values"));

        Assert.Equal("UnrealArray<int>", accessor?.TypeName);
        Assert.Equal("new UnrealArray<int>(this, 0x28, 0x4, \"Values\", static (context, element) => context.ValueAt<int>(element), static (context, value) => Bytes(value), null)", accessor?.GetBody);
        Assert.Null(accessor?.SetBody);
    }

    [Fact]
    public void AnObjectArrayReadsAndWritesEachElementAsItsType()
    {
        var element = new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "PalItem");
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x30, 16, Element: element, Name: "Items"));

        Assert.Equal("UnrealArray<PalItem?>", accessor?.TypeName);
        Assert.Equal("new UnrealArray<PalItem?>(this, 0x30, 0x8, \"Items\", static (context, element) => context.ObjectAt(element) as PalItem, static (context, value) => Bytes<nint>(value?.Address ?? 0), null)", accessor?.GetBody);
    }

    [Fact]
    public void AStructArrayReadsALiveViewAndWritesTheStructBytes()
    {
        var element = new SdkPropertyFacts(PropertyKind.Struct, 0x0, 24, "Vector");
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x40, 16, Element: element, Name: "Points"));

        Assert.Equal("UnrealArray<Vector>", accessor?.TypeName);
        Assert.Equal("new UnrealArray<Vector>(this, 0x40, 0x18, \"Points\", static (context, element) => new Vector(element, context), static (context, value) => context.BytesAt(value.Address, 0x18), null)", accessor?.GetBody);
    }

    [Fact]
    public void AStringArrayWritesThroughAReleasedTemporary()
    {
        var element = new SdkPropertyFacts(PropertyKind.Str, 0x0, 16);
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x50, 16, Element: element, Name: "Names"));

        Assert.Equal("UnrealArray<string>", accessor?.TypeName);
        Assert.Equal("new UnrealArray<string>(this, 0x50, 0x10, \"Names\", static (context, element) => context.StringAt(element), static (context, value) => context.StringValueBytes(value), static (context, bytes) => context.ReleaseStringValue(bytes))", accessor?.GetBody);
    }

    [Fact]
    public void ANameArrayWritesItsInternedBytesDirectly()
    {
        var element = new SdkPropertyFacts(PropertyKind.Name, 0x0, 8);
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x58, 16, Element: element, Name: "Tags"));

        Assert.Equal("new UnrealArray<string>(this, 0x58, 0x8, \"Tags\", static (context, element) => context.NameAt(element), static (context, value) => context.NameBytes(value), null)", accessor?.GetBody);
    }

    [Fact]
    public void ASetMapsToATypedViewWithItsBakedStride()
    {
        var element = new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4);
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Set, 0x28, 16, Element: element, Stride: 0x10, Name: "Tags"));

        Assert.Equal("UnrealSet<int>", accessor?.TypeName);
        Assert.Equal("new UnrealSet<int>(this, 0x28, 0x10, \"Tags\", static (context, element) => context.ValueAt<int>(element), static (context, value) => Bytes(value), null)", accessor?.GetBody);
        Assert.Null(accessor?.SetBody);
    }

    [Fact]
    public void AMapMapsToATypedViewWithKeyAndValueReadersAndWriters()
    {
        var key = new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4);
        var value = new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "PalItem");
        var accessor = SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Map, 0x30, 80, Key: key, Value: value, Stride: 0x50, ValueOffset: 0x8, Name: "Lookup"));

        Assert.Equal("UnrealMap<int, PalItem?>", accessor?.TypeName);
        Assert.Equal(
            "new UnrealMap<int, PalItem?>(this, 0x30, 0x50, 0x8, \"Lookup\", static (context, element) => context.ValueAt<int>(element), static (context, element) => context.ObjectAt(element) as PalItem, static (context, value) => Bytes(value), static (context, value) => Bytes<nint>(value?.Address ?? 0), null, null)",
            accessor?.GetBody);
    }

    [Fact]
    public void AnUnmappedKindReturnsNullSoTheExtractorSkipsIt()
    {
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Struct, 0x50, 24)));
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x40, 16)));
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Array, 0x40, 16, Element: new SdkPropertyFacts(PropertyKind.Struct, 0x0, 24))));
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.Map, 0x40, 80)));
        Assert.Null(SdkTypeMap.Map(new SdkPropertyFacts(PropertyKind.MulticastSparseDelegate, 0x40, 16)));
    }

    private static (string, string, string?)? Tuple(SdkAccessor? accessor)
    {
        return accessor is not null ? (accessor.TypeName, accessor.GetBody, accessor.SetBody) : null;
    }
}