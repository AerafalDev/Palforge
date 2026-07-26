namespace Palforge.Unreal.Reflection;

internal readonly struct ContainerLayout
{
    public nint KeyProperty { get; init; }

    public nint ValueProperty { get; init; }

    public int ValueOffset { get; init; }

    public int SlotStride { get; init; }

    public int HashNextIdOffset { get; init; }

    public int HashIndexOffset { get; init; }

    public bool IsMap { get; init; }

    public bool IsValid =>
        KeyProperty is not 0 && SlotStride > 0 && HashIndexOffset > 0 && (!IsMap || ValueProperty is not 0);
}