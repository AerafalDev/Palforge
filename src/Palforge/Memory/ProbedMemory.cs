using Windows.Win32;
using Windows.Win32.Foundation;

namespace Palforge.Memory;

internal sealed unsafe class ProbedMemory : IMemory
{
    private readonly HANDLE _process;

    public ProbedMemory()
    {
        _process = PInvoke.GetCurrentProcess();
    }

    public bool IsReadable(nint address, int length)
    {
        if (address is 0 || length < 0)
            return false;

        if (length is 0)
            return true;

        byte probe = 0;

        return Read(address, &probe, 1) && Read(address + length - 1, &probe, 1);
    }

    public bool TryRead<T>(nint address, out T value)
        where T : unmanaged
    {
        T local = default;

        if (!Read(address, &local, (nuint)sizeof(T)))
        {
            value = default;
            return false;
        }

        value = local;
        return true;
    }

    public bool TryRead(nint address, Span<byte> destination)
    {
        if (destination.IsEmpty)
            return true;

        fixed (byte* buffer = destination)
            return Read(address, buffer, (nuint)destination.Length);
    }

    public bool TryWrite<T>(nint address, in T value)
        where T : unmanaged
    {
        fixed (T* source = &value)
            return Write(address, source, (nuint)sizeof(T));
    }

    public bool TryWrite(nint address, ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
            return true;

        fixed (byte* buffer = source)
            return Write(address, buffer, (nuint)source.Length);
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

    private bool Read(nint address, void* destination, nuint length)
    {
        if (address is 0)
            return false;

        nuint read = 0;

        return PInvoke.ReadProcessMemory(_process, (void*)address, destination, length, &read) && read == length;
    }

    private bool Write(nint address, void* source, nuint length)
    {
        if (address is 0)
            return false;

        nuint written = 0;

        return PInvoke.WriteProcessMemory(_process, (void*)address, source, length, &written) && written == length;
    }
}