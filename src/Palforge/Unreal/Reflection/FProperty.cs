using System.Globalization;
using System.Runtime.CompilerServices;

namespace Palforge.Unreal.Reflection;

public class FProperty : FField
{
    public int ArrayDim =>
        Context.ArrayDimOf(Address);

    public int ElementSize =>
        Context.ElementSizeOf(Address);

    public EPropertyFlags Flags =>
        Context.PropertyFlagsOf(Address);

    public int Offset =>
        Context.OffsetInternalOf(Address);

    public PropertyKind Kind =>
        Context.ClassifyProperty(Address);

    internal FProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public T Get<T>(UObject target)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetAt<T>(target.Address);
    }

    public bool Set<T>(UObject target, in T value)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetAt(target.Address, value);
    }

    public bool TryGet<T>(UObject target, out T value)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(target);

        if (Unsafe.SizeOf<T>() != ElementSize)
        {
            value = default;
            return false;
        }

        return Context.TryReadValue(target.Address + Offset, out value);
    }

    public bool TrySet<T>(UObject target, in T value)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(target);

        return Unsafe.SizeOf<T>() == ElementSize && Context.WriteValue(target.Address + Offset, value);
    }

    public T GetAt<T>(nint container)
        where T : unmanaged
    {
        EnsureSize<T>();

        return Context.TryReadValue(container + Offset, out T value) ? value : default;
    }

    public bool SetAt<T>(nint container, in T value)
        where T : unmanaged
    {
        EnsureSize<T>();

        return Context.WriteValue(container + Offset, value);
    }

    public virtual string FormatValue(nint container)
    {
        return Kind switch
        {
            PropertyKind.Byte => GetAt<byte>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Int8 => GetAt<sbyte>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Int16 => GetAt<short>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Int32 => GetAt<int>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Int64 => GetAt<long>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.UInt16 => GetAt<ushort>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.UInt32 => GetAt<uint>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.UInt64 => GetAt<ulong>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Float => GetAt<float>(container).ToString(CultureInfo.InvariantCulture),
            PropertyKind.Double => GetAt<double>(container).ToString(CultureInfo.InvariantCulture),
            _ => $"<{Kind}>"
        };
    }

    private void EnsureSize<T>()
        where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();

        if (size != ElementSize)
            throw new InvalidOperationException($"'{Name}' holds {ElementSize} bytes but {typeof(T).Name} is {size} — wrong value type for this property");
    }
}