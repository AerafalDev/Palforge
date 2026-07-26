using System.Text;

namespace Palforge.Unreal.Reflection;

public sealed class FArrayProperty : FProperty
{
    private const int MaxRender = 16;

    public FProperty? Inner =>
        Context.WrapProperty(Context.InnerOf(Address));

    internal FArrayProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public int Count(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return CountAt(target.Address);
    }

    public int CountAt(nint container)
    {
        return Context.TryReadArray(container + Offset, out _, out var count) ? count : 0;
    }

    public nint ElementAt(nint container, int index)
    {
        if (Inner is not { } inner || !Context.TryReadArray(container + Offset, out var data, out var count) || (uint)index >= (uint)count)
            return 0;

        return data + (index * inner.ElementSize);
    }

    public int Add(UObject target, ReadOnlySpan<byte> element)
    {
        ArgumentNullException.ThrowIfNull(target);

        return AddAt(target.Address, element);
    }

    public int AddAt(nint container, ReadOnlySpan<byte> element)
    {
        return Inner is { } inner ? Context.ArrayInsert(container + Offset, inner.Address, inner.ElementSize, CountAt(container), element) : -1;
    }

    public int InsertAt(nint container, int index, ReadOnlySpan<byte> element)
    {
        return Inner is { } inner ? Context.ArrayInsert(container + Offset, inner.Address, inner.ElementSize, index, element) : -1;
    }

    public bool RemoveAt(nint container, int index, int count = 1)
    {
        return Inner is { } inner && Context.ArrayRemoveAt(container + Offset, inner.Address, inner.ElementSize, index, count);
    }

    public bool ClearAt(nint container)
    {
        return Inner is { } inner && Context.ArrayClear(container + Offset, inner.Address, inner.ElementSize);
    }

    public bool SetElementAt(nint container, int index, ReadOnlySpan<byte> element)
    {
        if (Inner is not { } inner || element.Length != inner.ElementSize)
            return false;

        var slot = ElementAt(container, index);

        return slot is not 0 && Context.PropertyAssignFrom(inner.Address, slot, element);
    }

    public override string FormatValue(nint container)
    {
        if (Inner is not { } inner)
            return $"<{Kind}>";

        if (!Context.TryReadArray(container + Offset, out var data, out var count))
            return "[]";

        var stride = inner.ElementSize;
        var shown = Math.Min(count, MaxRender);
        var builder = new StringBuilder("[");

        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append(inner.FormatValue(data + (index * stride)));
        }

        if (count > shown)
            builder.Append(", …").Append(count - shown).Append(" more");

        return builder.Append(']').ToString();
    }
}