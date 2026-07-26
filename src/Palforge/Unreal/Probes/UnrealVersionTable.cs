namespace Palforge.Unreal.Probes;

internal static class UnrealVersionTable
{
    public const string Version = "UE5.1";

    public static readonly IReadOnlyDictionary<string, int> Offsets = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [LayoutNames.ObjectFlags] = 0x08,
        [LayoutNames.InternalIndex] = 0x0C,
        [LayoutNames.ClassPrivate] = 0x10,
        [LayoutNames.NamePrivate] = 0x18,
        [LayoutNames.OuterPrivate] = 0x20,

        [LayoutNames.FieldNext] = 0x28,

        [LayoutNames.SuperStruct] = 0x40,
        [LayoutNames.Children] = 0x48,
        [LayoutNames.ChildProperties] = 0x50,
        [LayoutNames.PropertiesSize] = 0x58,
        [LayoutNames.MinAlignment] = 0x5C,

        [LayoutNames.ClassFlags] = 0xD4,
        [LayoutNames.ClassCastFlags] = 0xD8,
        [LayoutNames.ClassWithin] = 0xE0,
        [LayoutNames.ClassConfigName] = 0xE8,
        [LayoutNames.ClassDefaultObject] = 0x110,
        [LayoutNames.FuncMap] = 0x128,
        [LayoutNames.Interfaces] = 0x1D0,

        [LayoutNames.FunctionFlags] = 0xB0,
        [LayoutNames.NumParms] = 0xB4,
        [LayoutNames.ParmsSize] = 0xB6,
        [LayoutNames.ReturnValueOffset] = 0xB8,
        [LayoutNames.FunctionFunc] = 0xD8,

        [LayoutNames.StructFlags] = 0xB0,

        [LayoutNames.EnumCppType] = 0x30,
        [LayoutNames.EnumNames] = 0x48,
        [LayoutNames.EnumCppForm] = 0x58,
        [LayoutNames.EnumFlags] = 0x5C,

        [LayoutNames.FieldClassPrivate] = 0x08,
        [LayoutNames.FieldOwner] = 0x10,
        [LayoutNames.FieldChainNext] = 0x20,
        [LayoutNames.FieldNamePrivate] = 0x28,
        [LayoutNames.FieldFlagsPrivate] = 0x30,

        [LayoutNames.FieldClassName] = 0x00,
        [LayoutNames.FieldClassCastFlags] = 0x10,

        [LayoutNames.ArrayDim] = 0x38,
        [LayoutNames.ElementSize] = 0x3C,
        [LayoutNames.PropertyFlags] = 0x40,
        [LayoutNames.OffsetInternal] = 0x4C,
        [LayoutNames.RepNotifyFunc] = 0x50,
        [LayoutNames.PropertyBaseSize] = 0x78,

        [LayoutNames.ObjectsTable] = 0x10,
        [LayoutNames.NumElements] = 0x24,
        [LayoutNames.ElementsPerChunk] = 0x10000,
        [LayoutNames.ItemObject] = 0x00,
        [LayoutNames.ItemFlags] = 0x08,
        [LayoutNames.ItemSerialNumber] = 0x10,
        [LayoutNames.ItemStride] = 0x18,

        [LayoutNames.ProcessEventSlot] = 0x260
    };
}