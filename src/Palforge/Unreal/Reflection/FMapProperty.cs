using System.Text;

namespace Palforge.Unreal.Reflection;

public sealed class FMapProperty : FProperty
{
    private const int MaxRender = 16;

    public FProperty? Key =>
        Context.WrapProperty(Context.MapKeyOf(Address));

    public FProperty? Value =>
        Context.WrapProperty(Context.MapValueOf(Address));

    internal int PairStride =>
        Context.MapPairStrideOf(Address);

    internal int ValueOffset =>
        Context.MapValueOffsetOf(Address);

    internal FMapProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public int Count(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return CountAt(target.Address);
    }

    public int CountAt(nint container)
    {
        return Context.SparseCount(container + Offset);
    }

    public bool AddAt(nint container, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        if (Key is not { } k || Value is not { } v || key.Length != k.ElementSize || value.Length != v.ElementSize)
            return false;

        return Context.ContainerAddBytes(container + Offset, Context.MapLayoutOf(Address), key, value);
    }

    public bool RemoveAt(nint container, ReadOnlySpan<byte> key)
    {
        return Key is { } k && key.Length == k.ElementSize && Context.ContainerRemoveBytes(container + Offset, Context.MapLayoutOf(Address), key);
    }

    public bool ContainsKeyAt(nint container, ReadOnlySpan<byte> key)
    {
        return Key is { } k && key.Length == k.ElementSize && Context.ContainerContainsBytes(container + Offset, Context.MapLayoutOf(Address), key);
    }

    public override string FormatValue(nint container)
    {
        if (Key is not { } key || Value is not { } value)
            return $"<{Kind}>";

        var stride = Context.MapPairStrideOf(Address);
        var valueOffset = Context.MapValueOffsetOf(Address);
        var builder = new StringBuilder("{");
        var shown = 0;

        foreach (var element in Context.SparseElements(container + Offset, stride))
        {
            if (shown >= MaxRender)
            {
                builder.Append(", …");
                break;
            }

            if (shown > 0)
                builder.Append(", ");

            builder.Append(key.FormatValue(element)).Append(": ").Append(value.FormatValue(element + valueOffset));
            shown++;
        }

        return builder.Append('}').ToString();
    }
}