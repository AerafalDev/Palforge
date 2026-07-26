namespace Palforge.Unreal.Reflection;

public sealed class FStrProperty : FProperty
{
    internal FStrProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string GetString(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetStringAt(target.Address);
    }

    public string GetStringAt(nint container)
    {
        return Context.ReadFString(container + Offset);
    }

    public bool SetString(UObject target, string value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(value);

        return SetStringAt(target.Address, value);
    }

    public bool SetStringAt(nint container, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Context.WriteFString(container + Offset, value);
    }

    public override string FormatValue(nint container)
    {
        return $"\"{GetStringAt(container)}\"";
    }
}