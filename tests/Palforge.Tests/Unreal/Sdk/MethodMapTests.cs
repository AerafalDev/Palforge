using Palforge.Unreal.Reflection;
using Palforge.Unreal.Sdk;

namespace Palforge.Tests.Unreal.Sdk;

public sealed class MethodMapTests
{
    private const EPropertyFlags In = EPropertyFlags.Parm;
    private const EPropertyFlags Out = EPropertyFlags.Parm | EPropertyFlags.OutParm;
    private const EPropertyFlags Ref = EPropertyFlags.Parm | EPropertyFlags.OutParm | EPropertyFlags.ReferenceParm;
    private const EPropertyFlags ConstRef = EPropertyFlags.Parm | EPropertyFlags.OutParm | EPropertyFlags.ReferenceParm | EPropertyFlags.ConstParm;

    [Fact]
    public void AScalarInputParameterMarshalsItsBytes()
    {
        var parameter = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4), "count", In);

        Assert.Equal("int", parameter?.TypeName);
        Assert.Equal("count", parameter?.Name);
        Assert.Equal("Bytes(count)", parameter?.Marshal);
        Assert.Equal("", parameter?.Modifier);
        Assert.Null(parameter?.Output);
    }

    [Fact]
    public void AnObjectInputParameterMarshalsItsPointer()
    {
        var untyped = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Object, 0x0, 8), "target", In);
        var typed = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "PalPlayer"), "player", In);

        Assert.Equal("UObject?", untyped?.TypeName);
        Assert.Equal("Bytes<nint>(target?.Address ?? 0)", untyped?.Marshal);
        Assert.Equal("PalPlayer?", typed?.TypeName);
        Assert.Equal("Bytes<nint>(player?.Address ?? 0)", typed?.Marshal);
    }

    [Fact]
    public void AnOutParameterMarshalsAZeroBufferAndReadsBack()
    {
        var parameter = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4), "result", Out);

        Assert.Equal("out", parameter?.Modifier);
        Assert.Equal("Bytes<int>(default)", parameter?.Marshal);
        Assert.Equal("As<int>(#)", parameter?.Output);
    }

    [Fact]
    public void ARefParameterMarshalsItsValueAndReadsBack()
    {
        var parameter = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4), "value", Ref);

        Assert.Equal("ref", parameter?.Modifier);
        Assert.Equal("Bytes(value)", parameter?.Marshal);
        Assert.Equal("As<int>(#)", parameter?.Output);
    }

    [Fact]
    public void AConstRefParameterStaysAPlainInput()
    {
        var parameter = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4), "value", ConstRef);

        Assert.Equal("", parameter?.Modifier);
        Assert.Equal("Bytes(value)", parameter?.Marshal);
        Assert.Null(parameter?.Output);
    }

    [Fact]
    public void AnObjectReturnWrapsThePointerAsItsType()
    {
        var untyped = SdkMethodMap.Return(new SdkPropertyFacts(PropertyKind.Object, 0x0, 8));
        var typed = SdkMethodMap.Return(new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "PalPlayer"));

        Assert.Equal(("UObject?", "SdkEnv.Wrap(#)"), untyped);
        Assert.Equal(("PalPlayer?", "SdkEnv.Wrap(#) as PalPlayer"), typed);
    }

    [Fact]
    public void HeapInputParametersMarshalOneWayIntoTheFrame()
    {
        var name = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Name, 0x0, 8), "tag", In);
        var text = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Str, 0x0, 16), "label", In);
        var structArg = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Struct, 0x0, 24, "Vector"), "location", In);

        Assert.Equal("string", name?.TypeName);
        Assert.Equal("SdkEnv.NameBytes(tag)", name?.Marshal);
        Assert.Equal("string", text?.TypeName);
        Assert.Equal("StringArgument(label)", text?.Marshal);
        Assert.Equal("Vector", structArg?.TypeName);
        Assert.Equal("SdkEnv.StructBytes(location, 24)", structArg?.Marshal);
        Assert.Equal("", structArg?.Modifier);
        Assert.Null(structArg?.Output);
    }

    [Fact]
    public void ATextOutParameterReadsBackTheDecodedContent()
    {
        var text = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Str, 0x0, 16), "label", Out);
        var name = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Name, 0x0, 8), "tag", Ref);

        Assert.Equal("string", text?.TypeName);
        Assert.Equal("out", text?.Modifier);
        Assert.Equal("StringArgument(\"\")", text?.Marshal);
        Assert.Equal("SdkEnv.Text(#)", text?.Output);

        Assert.Equal("ref", name?.Modifier);
        Assert.Equal("SdkEnv.NameBytes(tag)", name?.Marshal);
        Assert.Equal("SdkEnv.Text(#)", name?.Output);
    }

    [Fact]
    public void AStructOutParameterIsFilledInPlaceThroughItsAddress()
    {
        var filled = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Struct, 0x0, 24, "Vector"), "v", Ref);

        Assert.Equal("Vector", filled?.TypeName);
        Assert.Equal("", filled?.Modifier);
        Assert.Equal("SdkEnv.StructBytes(v, 24)", filled?.Marshal);
        Assert.Equal("v.Address", filled?.Destination);
        Assert.Null(filled?.Output);
    }

    [Fact]
    public void AnUnsupportedParameterOrReturnIsNullSoTheFunctionIsSkipped()
    {
        Assert.Null(SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16), "items", In));
        Assert.Null(SdkMethodMap.Return(new SdkPropertyFacts(PropertyKind.Map, 0x0, 80)));
    }
}