namespace Palforge.Unreal.Reflection;

public sealed class FBoolProperty : FProperty
{
    public byte ByteOffset =>
        Context.BoolByteOffsetOf(Address);

    public byte FieldMask =>
        Context.BoolFieldMaskOf(Address);

    public bool IsNativeBool =>
        FieldMask is 0xFF;

    internal FBoolProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public bool GetBool(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetBoolAt(target.Address);
    }

    public bool SetBool(UObject target, bool value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetBoolAt(target.Address, value);
    }

    public bool GetBoolAt(nint container)
    {
        var raw = Context.TryReadValue(container + Offset + ByteOffset, out byte value) ? value : (byte)0;

        return (raw & FieldMask) is not 0;
    }

    public bool SetBoolAt(nint container, bool value)
    {
        var address = container + Offset + ByteOffset;
        var mask = FieldMask;
        var raw = Context.TryReadValue(address, out byte current) ? current : (byte)0;

        return Context.WriteValue(address, (byte)(value ? raw | mask : raw & ~mask));
    }

    public override string FormatValue(nint container)
    {
        return GetBoolAt(container) ? "true" : "false";
    }
}