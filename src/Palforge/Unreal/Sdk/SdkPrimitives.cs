using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal static class SdkPrimitives
{
    public static string? Scalar(PropertyKind kind)
    {
        return kind switch
        {
            PropertyKind.Byte => "byte",
            PropertyKind.Int8 => "sbyte",
            PropertyKind.Int16 => "short",
            PropertyKind.Int32 => "int",
            PropertyKind.Int64 => "long",
            PropertyKind.UInt16 => "ushort",
            PropertyKind.UInt32 => "uint",
            PropertyKind.UInt64 => "ulong",
            PropertyKind.Float => "float",
            PropertyKind.Double => "double",
            _ => null
        };
    }

    public static string EnumUnderlying(int elementSize)
    {
        return elementSize switch
        {
            1 => "byte",
            2 => "ushort",
            8 => "long",
            _ => "int"
        };
    }
}