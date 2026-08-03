using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal static class SdkMethodMap
{
    public const string CallPlaceholder = "#";

    public static SdkParameter? Parameter(SdkPropertyFacts facts, string name, EPropertyFlags flags)
    {
        var isOut = (flags & EPropertyFlags.OutParm) is not 0 && (flags & EPropertyFlags.ConstParm) is 0;

        if (isOut)
        {
            if (Core(facts) is { } core)
            {
                return (flags & EPropertyFlags.ReferenceParm) is not 0
                    ? new SdkParameter(name, core.Type, Input(facts, name), "ref", core.Output)
                    : new SdkParameter(name, core.Type, core.Zero, "out", core.Output);
            }

            if (facts is { Kind: PropertyKind.Struct, ReferencedType: { } filled })
                return new SdkParameter(name, filled, $"SdkEnv.StructBytes({name}, {facts.ElementSize})", Destination: $"{name}.Address");

            if (facts is { Kind: PropertyKind.Array, Element: { } element } && ElementList(element) is { } list)
                return new SdkParameter(name, list.Type, $"new byte[{facts.ElementSize}]", "out", list.Reader);

            return null;
        }

        if (Core(facts) is { } inputCore)
            return new SdkParameter(name, inputCore.Type, Input(facts, name));

        return facts switch
        {
            { Kind: PropertyKind.Struct, ReferencedType: { } structType } => new SdkParameter(name, structType, $"SdkEnv.StructBytes({name}, {facts.ElementSize})"),
            _ => null
        };
    }

    public static (string Type, string Reader)? Return(SdkPropertyFacts facts)
    {
        return Core(facts) is { } core ? (core.Type, core.Output) : null;
    }

    private static (string Type, string Reader)? ElementList(SdkPropertyFacts element)
    {
        if (SdkPrimitives.Scalar(element.Kind) is { } scalar)
            return ($"{scalar}[]", $"SdkEnv.Values<{scalar}>({CallPlaceholder})");

        return element switch
        {
            { Kind: PropertyKind.Object or PropertyKind.Class, ReferencedType: { } referenced } => ($"{referenced}[]", $"SdkEnv.Objects<{referenced}>({CallPlaceholder})"),
            { Kind: PropertyKind.Object or PropertyKind.Class } => ("UObject[]", $"SdkEnv.Objects<UObject>({CallPlaceholder})"),
            { Kind: PropertyKind.Enum, ReferencedType: { } enumType } => ($"{enumType}[]", $"SdkEnv.Values<{SdkPrimitives.EnumUnderlying(element.ElementSize)}>({CallPlaceholder}).Select(static value => ({enumType})value).ToArray()"),
            _ => null
        };
    }

    private static (string Type, string Zero, string Output)? Core(SdkPropertyFacts facts)
    {
        if (SdkPrimitives.Scalar(facts.Kind) is { } scalar)
            return (scalar, $"Bytes<{scalar}>(default)", $"As<{scalar}>({CallPlaceholder})");

        return facts switch
        {
            { Kind: PropertyKind.Bool } => ("bool", "Bytes<byte>(0)", $"As<byte>({CallPlaceholder}) is not 0"),
            { Kind: PropertyKind.Enum, ReferencedType: { } enumType } => EnumCore(enumType, facts.ElementSize),
            { Kind: PropertyKind.Object or PropertyKind.Class } => (ObjectType(facts), "Bytes<nint>(0)", ObjectReader(facts)),
            { Kind: PropertyKind.Name } => ("string", "Bytes<long>(0)", $"SdkEnv.Text({CallPlaceholder})"),
            { Kind: PropertyKind.Str } => ("string", "StringArgument(\"\")", $"SdkEnv.Text({CallPlaceholder})"),
            _ => null
        };
    }

    private static (string Type, string Zero, string Output) EnumCore(string type, int elementSize)
    {
        var underlying = SdkPrimitives.EnumUnderlying(elementSize);

        return (type, $"Bytes<{underlying}>(default)", $"({type})As<{underlying}>({CallPlaceholder})");
    }

    private static string Input(SdkPropertyFacts facts, string name)
    {
        if (SdkPrimitives.Scalar(facts.Kind) is not null)
            return $"Bytes({name})";

        return facts switch
        {
            { Kind: PropertyKind.Bool } => $"Bytes<byte>({name} ? (byte)1 : (byte)0)",
            { Kind: PropertyKind.Enum } => $"Bytes(({SdkPrimitives.EnumUnderlying(facts.ElementSize)}){name})",
            { Kind: PropertyKind.Name } => $"SdkEnv.NameBytes({name})",
            { Kind: PropertyKind.Str } => $"StringArgument({name})",
            _ => $"Bytes<nint>({name}?.Address ?? 0)"
        };
    }

    private static string ObjectReader(SdkPropertyFacts facts)
    {
        return facts.ReferencedType is { } cast ? $"SdkEnv.Wrap({CallPlaceholder}) as {cast}" : $"SdkEnv.Wrap({CallPlaceholder})";
    }

    private static string ObjectType(SdkPropertyFacts facts)
    {
        return (facts.ReferencedType ?? "UObject") + "?";
    }
}