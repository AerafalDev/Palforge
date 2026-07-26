using System.Text;

namespace Palforge.Unreal.Reflection;

public sealed class FSetProperty : FProperty
{
    private const int MaxRender = 16;

    public FProperty? Element =>
        Context.WrapProperty(Context.SetElementOf(Address));

    internal int Stride =>
        Context.SetStrideOf(Address);

    internal FSetProperty(nint address, UnrealContext context)
        : base(address, context)
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

    public bool AddAt(nint container, ReadOnlySpan<byte> element)
    {
        return Element is { } inner && element.Length == inner.ElementSize && Context.ContainerAddBytes(container + Offset, Context.SetLayoutOf(Address), element, default);
    }

    public bool RemoveAt(nint container, ReadOnlySpan<byte> element)
    {
        return Element is { } inner && element.Length == inner.ElementSize && Context.ContainerRemoveBytes(container + Offset, Context.SetLayoutOf(Address), element);
    }

    public bool ContainsAt(nint container, ReadOnlySpan<byte> element)
    {
        return Element is { } inner && element.Length == inner.ElementSize && Context.ContainerContainsBytes(container + Offset, Context.SetLayoutOf(Address), element);
    }

    public override string FormatValue(nint container)
    {
        if (Element is not { } element)
            return $"<{Kind}>";

        var stride = Context.SetStrideOf(Address);
        var builder = new StringBuilder("{");
        var shown = 0;

        foreach (var slot in Context.SparseElements(container + Offset, stride))
        {
            if (shown >= MaxRender)
            {
                builder.Append(", …");
                break;
            }

            if (shown > 0)
                builder.Append(", ");

            builder.Append(element.FormatValue(slot));
            shown++;
        }

        return builder.Append('}').ToString();
    }
}