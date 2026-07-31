using System.Runtime.InteropServices;
using Palforge.Memory;

namespace Palforge.Tests.Memory;

internal sealed class BufferMemory : IMemory
{
    private readonly byte[] _data;
    private readonly nint _baseAddress;

    public BufferMemory(byte[] data, nint baseAddress)
    {
        _data = data;
        _baseAddress = baseAddress;
    }

    public bool IsReadable(nint address, int length)
    {
        return address is not 0 && length >= 0 && TryGetOffset(address, length, out _);
    }

    public bool TryRead<T>(nint address, out T value)
        where T : unmanaged
    {
        value = default;

        return TryRead(address, MemoryMarshal.AsBytes(new Span<T>(ref value)));
    }

    public bool TryRead(nint address, Span<byte> destination)
    {
        if (destination.IsEmpty)
            return true;

        if (!TryGetOffset(address, destination.Length, out var offset))
            return false;

        _data.AsSpan(offset, destination.Length).CopyTo(destination);

        return true;
    }

    public bool TryWrite<T>(nint address, in T value)
        where T : unmanaged
    {
        var local = value;

        return TryWrite(address, MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in local)));
    }

    public bool TryWrite(nint address, ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
            return true;

        if (!TryGetOffset(address, source.Length, out var offset))
            return false;

        source.CopyTo(_data.AsSpan(offset, source.Length));

        return true;
    }

    public bool WriteProtected<T>(nint address, in T value)
        where T : unmanaged
    {
        return TryWrite(address, value);
    }

    public bool WriteProtected(nint address, ReadOnlySpan<byte> source)
    {
        return TryWrite(address, source);
    }

    private bool TryGetOffset(nint address, int length, out int offset)
    {
        offset = 0;

        if (address < _baseAddress)
            return false;

        var relative = address - _baseAddress;

        if (relative > _data.Length - length)
            return false;

        offset = (int)relative;

        return true;
    }
}