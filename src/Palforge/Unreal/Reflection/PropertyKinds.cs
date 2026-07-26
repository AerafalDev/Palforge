namespace Palforge.Unreal.Reflection;

internal static class PropertyKinds
{
    public static PropertyKind FromCastFlags(EClassCastFlags flags)
    {
        return flags switch
        {
            _ when Has(flags, EClassCastFlags.BoolProperty) => PropertyKind.Bool,
            _ when Has(flags, EClassCastFlags.EnumProperty) => PropertyKind.Enum,
            _ when Has(flags, EClassCastFlags.ByteProperty) => PropertyKind.Byte,
            _ when Has(flags, EClassCastFlags.Int8Property) => PropertyKind.Int8,
            _ when Has(flags, EClassCastFlags.Int16Property) => PropertyKind.Int16,
            _ when Has(flags, EClassCastFlags.IntProperty) => PropertyKind.Int32,
            _ when Has(flags, EClassCastFlags.Int64Property) => PropertyKind.Int64,
            _ when Has(flags, EClassCastFlags.UInt16Property) => PropertyKind.UInt16,
            _ when Has(flags, EClassCastFlags.UInt32Property) => PropertyKind.UInt32,
            _ when Has(flags, EClassCastFlags.UInt64Property) => PropertyKind.UInt64,
            _ when Has(flags, EClassCastFlags.FloatProperty) => PropertyKind.Float,
            _ when Has(flags, EClassCastFlags.DoubleProperty) => PropertyKind.Double,
            _ when Has(flags, EClassCastFlags.NameProperty) => PropertyKind.Name,
            _ when Has(flags, EClassCastFlags.StrProperty) => PropertyKind.Str,
            _ when Has(flags, EClassCastFlags.TextProperty) => PropertyKind.Text,
            _ when Has(flags, EClassCastFlags.StructProperty) => PropertyKind.Struct,
            _ when Has(flags, EClassCastFlags.SoftClassProperty) => PropertyKind.SoftClass,
            _ when Has(flags, EClassCastFlags.SoftObjectProperty) => PropertyKind.SoftObject,
            _ when Has(flags, EClassCastFlags.WeakObjectProperty) => PropertyKind.WeakObject,
            _ when Has(flags, EClassCastFlags.LazyObjectProperty) => PropertyKind.LazyObject,
            _ when Has(flags, EClassCastFlags.ClassPtrProperty) => PropertyKind.Class,
            _ when Has(flags, EClassCastFlags.ClassProperty) => PropertyKind.Class,
            _ when Has(flags, EClassCastFlags.ObjectPtrProperty) => PropertyKind.Object,
            _ when Has(flags, EClassCastFlags.ObjectProperty) => PropertyKind.Object,
            _ when Has(flags, EClassCastFlags.InterfaceProperty) => PropertyKind.Interface,
            _ when Has(flags, EClassCastFlags.MulticastInlineDelegateProperty) => PropertyKind.MulticastInlineDelegate,
            _ when Has(flags, EClassCastFlags.MulticastSparseDelegateProperty) => PropertyKind.MulticastSparseDelegate,
            _ when Has(flags, EClassCastFlags.DelegateProperty) => PropertyKind.Delegate,
            _ when Has(flags, EClassCastFlags.ArrayProperty) => PropertyKind.Array,
            _ when Has(flags, EClassCastFlags.MapProperty) => PropertyKind.Map,
            _ when Has(flags, EClassCastFlags.SetProperty) => PropertyKind.Set,
            _ when Has(flags, EClassCastFlags.FieldPathProperty) => PropertyKind.FieldPath,
            _ when Has(flags, EClassCastFlags.OptionalProperty) => PropertyKind.Optional,
            _ => PropertyKind.Unknown
        };
    }

    private static bool Has(EClassCastFlags flags, EClassCastFlags bit)
    {
        return (flags & bit) is not 0;
    }
}