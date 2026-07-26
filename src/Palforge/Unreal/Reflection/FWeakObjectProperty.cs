namespace Palforge.Unreal.Reflection;

public sealed class FWeakObjectProperty : FProperty
{
    internal FWeakObjectProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public UObject? GetObject(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetObjectAt(target.Address);
    }

    public UObject? GetObjectAt(nint container)
    {
        return Context.WeakObject(container + Offset);
    }

    public bool SetObject(UObject target, UObject? value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetObjectAt(target.Address, value);
    }

    public bool SetObjectAt(nint container, UObject? value)
    {
        return Context.WriteWeakObject(container + Offset, value?.Address ?? 0);
    }

    public override string FormatValue(nint container)
    {
        return GetObjectAt(container) is { } value ? $"{value.Class?.Name ?? "?"}'{value.Name}'" : "None";
    }
}