using System.Globalization;

namespace Palforge.Unreal.Reflection;

public sealed class FEnumProperty : FProperty
{
    public UEnum? Enum =>
        Context.WrapEnum(Context.EnumOf(Address));

    internal FEnumProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public long GetValue(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetValueAt(target.Address);
    }

    public long GetValueAt(nint container)
    {
        return ElementSize switch
        {
            1 => Context.TryReadValue(container + Offset, out byte value) ? value : 0,
            2 => Context.TryReadValue(container + Offset, out ushort value) ? value : 0,
            4 => Context.TryReadValue(container + Offset, out int value) ? value : 0,
            8 => Context.TryReadValue(container + Offset, out long value) ? value : 0,
            _ => 0
        };
    }

    public bool SetValue(UObject target, long value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetValueAt(target.Address, value);
    }

    public bool SetValueAt(nint container, long value)
    {
        return ElementSize switch
        {
            1 => Context.WriteValue(container + Offset, (byte)value),
            2 => Context.WriteValue(container + Offset, (ushort)value),
            4 => Context.WriteValue(container + Offset, (int)value),
            8 => Context.WriteValue(container + Offset, value),
            _ => false
        };
    }

    public override string FormatValue(nint container)
    {
        var value = GetValueAt(container);

        return Enum?.GetNameByValue(value) ?? value.ToString(CultureInfo.InvariantCulture);
    }
}