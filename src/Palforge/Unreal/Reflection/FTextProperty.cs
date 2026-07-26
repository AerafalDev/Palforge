namespace Palforge.Unreal.Reflection;

public sealed class FTextProperty : FProperty
{
    internal FTextProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string GetText(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetTextAt(target.Address);
    }

    public string GetTextAt(nint container)
    {
        return Context.TextValue(container + Offset);
    }

    public bool SetText(UObject target, string value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetTextAt(target.Address, value);
    }

    public bool SetTextAt(nint container, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Context.WriteText(Address, container + Offset, value);
    }

    public override string FormatValue(nint container)
    {
        return $"\"{GetTextAt(container)}\"";
    }
}