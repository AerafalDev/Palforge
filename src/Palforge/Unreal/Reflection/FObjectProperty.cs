namespace Palforge.Unreal.Reflection;

public class FObjectProperty : FProperty
{
    private const int ObjectProbe = 0x20;

    public UClass? PropertyClass =>
        Context.AsClass(Context.InnerOf(Address));

    internal FObjectProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public UObject? GetObject(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetObjectAt(target.Address);
    }

    public bool SetObject(UObject target, UObject? value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetObjectAt(target.Address, value);
    }

    public UObject? GetObjectAt(nint container)
    {
        if (!Context.TryReadValue(container + Offset, out nint pointer) || pointer is 0 || !Context.IsReadable(pointer, ObjectProbe))
            return null;

        return Context.Wrap(pointer);
    }

    public bool SetObjectAt(nint container, UObject? value)
    {
        return Context.WriteValue(container + Offset, value?.Address ?? 0);
    }

    public override string FormatValue(nint container)
    {
        return GetObjectAt(container) is { } value ? $"{value.Class?.Name ?? "?"}'{value.Name}'" : "null";
    }
}