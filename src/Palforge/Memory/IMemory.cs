namespace Palforge.Memory;

internal interface IMemory
{
    bool IsReadable(nint address, int length);

    bool TryRead<T>(nint address, out T value)
        where T : unmanaged;

    bool TryRead(nint address, Span<byte> destination);

    bool TryWrite<T>(nint address, in T value)
        where T : unmanaged;

    bool TryWrite(nint address, ReadOnlySpan<byte> source);

    bool WriteProtected<T>(nint address, in T value)
        where T : unmanaged;

    bool WriteProtected(nint address, ReadOnlySpan<byte> source);
}