using System.Globalization;

namespace Palforge.Unreal.Reflection;

public sealed class FByteProperty : FProperty
{
    public UEnum? Enum =>
        Context.WrapEnum(Context.ByteEnumOf(Address));

    internal FByteProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public byte GetByte(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetByteAt(target.Address);
    }

    public byte GetByteAt(nint container)
    {
        return Context.TryReadValue(container + Offset, out byte value) ? value : (byte)0;
    }

    public bool SetByte(UObject target, byte value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetByteAt(target.Address, value);
    }

    public bool SetByteAt(nint container, byte value)
    {
        return Context.WriteValue(container + Offset, value);
    }

    public override string FormatValue(nint container)
    {
        var value = GetByteAt(container);

        return Enum?.GetNameByValue(value) ?? value.ToString(CultureInfo.InvariantCulture);
    }
}