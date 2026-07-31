namespace Palforge.Tests.Unreal.Probes;

internal sealed class FakeLayout
{
    public int ObjectFlags { get; init; } = 0x10;
    public int InternalIndex { get; init; } = 0x14;
    public int ClassPrivate { get; init; } = 0x20;
    public int NamePrivate { get; init; } = 0x28;
    public int OuterPrivate { get; init; } = 0x30;
    public int ObjectSize { get; init; } = 0x40;

    public int FieldNext { get; init; } = 0x40;
    public int FieldSize { get; init; } = 0x50;

    public int SuperStruct { get; init; } = 0x50;
    public int Children { get; init; } = 0x60;
    public int ChildProperties { get; init; } = 0x68;
    public int PropertiesSize { get; init; } = 0x70;
    public int MinAlignment { get; init; } = 0x74;
    public int PropertyLink { get; init; } = 0x80;
    public int StructSize { get; init; } = 0x100;

    public int ClassFlags { get; init; } = 0x100;
    public int ClassCastFlags { get; init; } = 0x108;
    public int ClassDefaultObject { get; init; } = 0x118;
    public int ClassWithin { get; init; } = 0x120;
    public int ClassSize { get; init; } = 0x180;

    public int FFieldClassPrivate { get; init; } = 0x10;
    public int FFieldOwner { get; init; } = 0x18;
    public int FFieldNext { get; init; } = 0x28;
    public int FFieldNamePrivate { get; init; } = 0x30;
    public int FFieldFlags { get; init; } = 0x38;
    public int FFieldSize { get; init; } = 0x40;

    public int ArrayDim { get; init; } = 0x40;
    public int ElementSize { get; init; } = 0x44;
    public int PropertyFlags { get; init; } = 0x48;
    public int RepIndex { get; init; } = 0x50;
    public int OffsetInternal { get; init; } = 0x54;
    public int PropertyLinkNext { get; init; } = 0x58;
    public int PropertyBaseSize { get; init; } = 0x80;

    public int FieldClassName { get; init; } = 0x08;
    public int FieldClassCastFlags { get; init; } = 0x10;
    public int FieldClassSize { get; init; } = 0x40;

    public int ItemObject { get; init; }
    public int ItemFlags { get; init; } = 0x08;
    public int ItemSerialNumber { get; init; } = 0x10;
    public int ItemStride { get; init; } = 0x20;

    public int ChunkedObjects { get; init; }
    public int ChunkedMaxElements { get; init; } = 0x18;
    public int ChunkedNumElements { get; init; } = 0x1C;
    public int ChunkedMaxChunks { get; init; } = 0x20;
    public int ChunkedNumChunks { get; init; } = 0x24;
    public int ElementsPerChunk { get; init; } = 64;
    public int MaxChunks { get; init; } = 8;

    public int ObjObjects { get; init; } = 0x28;
    public int ObjectArraySize { get; init; } = 0x80;

    public int FunctionNumParms { get; init; } = 0xE0;
    public int FunctionParmsSize { get; init; } = 0xE4;

    public int EnumNames { get; init; } = 0x50;
    public int EnumPairStride { get; init; } = 0x10;
}