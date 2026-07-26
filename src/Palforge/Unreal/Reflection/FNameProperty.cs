namespace Palforge.Unreal.Reflection;

public sealed class FNameProperty : FProperty
{
    internal FNameProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string GetName(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetNameAt(target.Address);
    }

    public string GetNameAt(nint container)
    {
        return Context.TryReadValue(container + Offset, out int comparisonIndex) ? Context.ResolveName(comparisonIndex) : string.Empty;
    }

    public bool SetName(UObject target, string name)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetNameAt(target.Address, name);
    }

    public bool SetNameAt(nint container, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Context.WriteName(container + Offset, name);
    }

    public override string FormatValue(nint container)
    {
        return GetNameAt(container);
    }
}