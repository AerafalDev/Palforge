using System.Text;

namespace Palforge.Unreal.Reflection;

public sealed class FStructProperty : FProperty
{
    public UScriptStruct? Struct =>
        Context.WrapScriptStruct(Context.StructOf(Address));

    internal FStructProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public byte[] GetValue(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetValueAt(target.Address);
    }

    public byte[] GetValueAt(nint container)
    {
        var bytes = new byte[ElementSize];

        return Context.ReadBytes(container + Offset, bytes) ? bytes : [];
    }

    public bool SetValue(UObject target, ReadOnlySpan<byte> value)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetValueAt(target.Address, value);
    }

    public bool SetValueAt(nint container, ReadOnlySpan<byte> value)
    {
        return value.Length == ElementSize && Context.PropertyAssignFrom(Address, container + Offset, value);
    }

    public override string FormatValue(nint container)
    {
        if (Struct is not { } type)
            return $"<{Kind}>";

        var value = container + Offset;
        var builder = new StringBuilder("{");
        var first = true;

        foreach (var member in type.Properties)
        {
            if (!first)
                builder.Append(", ");

            builder.Append(member.Name).Append('=').Append(member.FormatValue(value));
            first = false;
        }

        return builder.Append('}').ToString();
    }
}