using System.Globalization;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal static class SdkTypeMap
{
    public static SdkAccessor? Map(SdkPropertyFacts facts)
    {
        var offset = Hex(facts.Offset);

        if (SdkPrimitives.Scalar(facts.Kind) is { } scalar)
            return new SdkAccessor(scalar, $"ReadAt<{scalar}>({offset})", $"WriteAt({offset}, value)");

        return facts.Kind switch
        {
            PropertyKind.Bool => new SdkAccessor("bool", $"ReadBoolAt({offset}, {Hex(facts.BoolMask)})", $"WriteBoolAt({offset}, {Hex(facts.BoolMask)}, value)"),
            PropertyKind.Enum when facts.ReferencedType is { } enumType => Enum(enumType, facts.ElementSize, offset),
            PropertyKind.Name => new SdkAccessor("string", $"ReadNameAt({offset})", $"WriteNameAt({offset}, value)"),
            PropertyKind.Str => new SdkAccessor("string", $"ReadStringAt({offset})", $"WriteStringAt({offset}, value)"),
            PropertyKind.Text => new SdkAccessor("string", $"ReadTextAt({offset})", null),
            PropertyKind.Object or PropertyKind.Class => Object(facts.ReferencedType, offset),
            PropertyKind.Struct when facts.ReferencedType is { } structType => new SdkAccessor(structType, $"new {structType}(Address + {offset}, Context)", null),
            PropertyKind.SoftObject or PropertyKind.SoftClass => new SdkAccessor("string", $"ReadSoftPathAt({offset})", $"WriteSoftPathAt({offset}, value)"),
            PropertyKind.WeakObject => new SdkAccessor("UObject?", $"ReadWeakAt({offset})", $"WriteWeakAt({offset}, value)"),
            PropertyKind.LazyObject => new SdkAccessor("string", $"ReadLazyGuidAt({offset})", null),
            PropertyKind.Interface => new SdkAccessor("UObject?", $"WrapAt({offset})", null),
            PropertyKind.Delegate => new SdkAccessor("UnrealDelegate", $"new UnrealDelegate(this, {offset})", null),
            PropertyKind.MulticastInlineDelegate when facts.Name is { } inlineName => Multicast(inlineName, offset, sparse: false),
            PropertyKind.MulticastSparseDelegate when facts.Name is { } sparseName => Multicast(sparseName, offset, sparse: true),
            PropertyKind.Array when facts.Element is { } element && Element(element) is { } read => new SdkAccessor($"UnrealArray<{read.Type}>", $"new UnrealArray<{read.Type}>(this, {offset}, {Hex(element.ElementSize)}, \"{facts.Name}\", {read.Reader}, {read.Writer ?? "null"}, {read.Release ?? "null"})", null),
            PropertyKind.Set when facts.Element is { } setElement && Element(setElement) is { } read => new SdkAccessor($"UnrealSet<{read.Type}>", $"new UnrealSet<{read.Type}>(this, {offset}, {Hex(facts.Stride)}, \"{facts.Name}\", {read.Reader}, {read.Writer ?? "null"}, {read.Release ?? "null"})", null),
            PropertyKind.Map when facts is { Key: { } key, Value: { } value } && Element(key) is { } keyRead && Element(value) is { } valueRead => new SdkAccessor($"UnrealMap<{keyRead.Type}, {valueRead.Type}>", $"new UnrealMap<{keyRead.Type}, {valueRead.Type}>(this, {offset}, {Hex(facts.Stride)}, {Hex(facts.ValueOffset)}, \"{facts.Name}\", {keyRead.Reader}, {valueRead.Reader}, {keyRead.Writer ?? "null"}, {valueRead.Writer ?? "null"}, {keyRead.Release ?? "null"}, {valueRead.Release ?? "null"})", null),
            _ => null
        };
    }

    private static SdkAccessor Multicast(string name, string offset, bool sparse)
    {
        return new SdkAccessor("UnrealMulticastDelegate", $"new UnrealMulticastDelegate(this, {offset}, \"{name}\", {(sparse ? "true" : "false")})", null);
    }

    private static (string Type, string Reader, string? Writer, string? Release)? Element(SdkPropertyFacts facts)
    {
        if (SdkPrimitives.Scalar(facts.Kind) is { } scalar)
            return (scalar, $"static (context, element) => context.ValueAt<{scalar}>(element)", "static (context, value) => Bytes(value)", null);

        return facts switch
        {
            { Kind: PropertyKind.Bool } => ("bool", "static (context, element) => context.ValueAt<byte>(element) is not 0", "static (context, value) => Bytes<byte>(value ? (byte)1 : (byte)0)", null),
            { Kind: PropertyKind.Enum, ReferencedType: { } enumType } => (enumType, $"static (context, element) => ({enumType})context.ValueAt<{SdkPrimitives.EnumUnderlying(facts.ElementSize)}>(element)", $"static (context, value) => Bytes(({SdkPrimitives.EnumUnderlying(facts.ElementSize)})value)", null),
            { Kind: PropertyKind.Object or PropertyKind.Class } => ElementObject(facts),
            { Kind: PropertyKind.Struct, ReferencedType: { } structType } => (structType, $"static (context, element) => new {structType}(element, context)", $"static (context, value) => context.BytesAt(value.Address, {Hex(facts.ElementSize)})", null),
            { Kind: PropertyKind.Name } => ("string", "static (context, element) => context.NameAt(element)", "static (context, value) => context.NameBytes(value)", null),
            { Kind: PropertyKind.Str } => ("string", "static (context, element) => context.StringAt(element)", "static (context, value) => context.StringValueBytes(value)", "static (context, bytes) => context.ReleaseStringValue(bytes)"),
            _ => null
        };
    }

    private static (string Type, string Reader, string? Writer, string? Release) ElementObject(SdkPropertyFacts facts)
    {
        var type = (facts.ReferencedType ?? "UObject") + "?";

        var reader = facts.ReferencedType is { } cast
            ? $"static (context, element) => context.ObjectAt(element) as {cast}"
            : "static (context, element) => context.ObjectAt(element)";

        return (type, reader, "static (context, value) => Bytes<nint>(value?.Address ?? 0)", null);
    }

    private static SdkAccessor Object(string? referencedType, string offset)
    {
        var type = referencedType is not null ? referencedType + "?" : "UObject?";
        var get = referencedType is not null ? $"WrapAt({offset}) as {referencedType}" : $"WrapAt({offset})";

        return new SdkAccessor(type, get, $"WriteObjectAt({offset}, value)");
    }

    private static SdkAccessor Enum(string type, int elementSize, string offset)
    {
        var underlying = SdkPrimitives.EnumUnderlying(elementSize);

        return new SdkAccessor(type, $"({type})ReadAt<{underlying}>({offset})", $"WriteAt({offset}, ({underlying})value)");
    }

    private static string Hex(int value)
    {
        return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
    }
}