using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Palforge.Unreal.Reflection;

public abstract class UnrealValueBase
{
    public nint Address { get; }

    internal UnrealContext Context { get; }

    private protected UnrealValueBase(nint address, UnrealContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Address = address;
        Context = context;
    }

    internal abstract FProperty? FindOwnProperty(string name);

    protected T ReadAt<T>(int offset)
        where T : unmanaged
    {
        return Context.TryReadValue(Address + offset, out T value) ? value : default;
    }

    protected bool WriteAt<T>(int offset, in T value)
        where T : unmanaged
    {
        return Context.WriteValue(Address + offset, value);
    }

    protected UObject? WrapAt(int offset)
    {
        return Context.TryReadValue(Address + offset, out nint pointer) && pointer is not 0 ? Context.WrapOrNull(pointer) : null;
    }

    protected bool WriteObjectAt(int offset, UObject? value)
    {
        return WriteAt(offset, value?.Address ?? 0);
    }

    protected bool ReadBoolAt(int offset, byte mask)
    {
        return (ReadAt<byte>(offset) & mask) is not 0;
    }

    protected bool WriteBoolAt(int offset, byte mask, bool value)
    {
        var raw = ReadAt<byte>(offset);

        return WriteAt(offset, (byte)(value ? raw | mask : raw & ~mask));
    }

    protected string ReadNameAt(int offset)
    {
        return Context.NameAtSlot(Address + offset);
    }

    protected bool WriteNameAt(int offset, string value)
    {
        return Context.WriteName(Address + offset, value);
    }

    protected string ReadStringAt(int offset)
    {
        return Context.ReadFString(Address + offset);
    }

    protected bool WriteStringAt(int offset, string value)
    {
        return Context.WriteFString(Address + offset, value);
    }

    protected string ReadTextAt(int offset)
    {
        return Context.TextValue(Address + offset);
    }

    protected string ReadSoftPathAt(int offset)
    {
        return Context.SoftObjectPath(Address + offset);
    }

    protected UObject? ReadWeakAt(int offset)
    {
        return Context.WeakObject(Address + offset);
    }

    protected bool WriteSoftPathAt(int offset, string value)
    {
        return Context.WriteSoftObjectPath(Address + offset, value);
    }

    protected bool WriteWeakAt(int offset, UObject? value)
    {
        return Context.WriteWeakObject(Address + offset, value?.Address ?? 0);
    }

    protected string ReadLazyGuidAt(int offset)
    {
        return Context.LazyGuid(Address + offset);
    }

    protected string ReadDelegateAt(int offset)
    {
        return Context.ScriptDelegate(Address + offset);
    }

    protected string ReadMulticastAt(int offset)
    {
        return Context.MulticastList(Address + offset);
    }

    protected static byte[] StringArgument(string value)
    {
        return Encoding.Unicode.GetBytes(value);
    }

    protected static byte[] Bytes<T>(in T value)
        where T : unmanaged
    {
        var bytes = new byte[Unsafe.SizeOf<T>()];

        MemoryMarshal.Write(bytes, in value);

        return bytes;
    }

    protected static T As<T>(byte[]? data)
        where T : unmanaged
    {
        return data is { } bytes && bytes.Length >= Unsafe.SizeOf<T>() ? MemoryMarshal.Read<T>(bytes) : default;
    }
}