using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.System.Memory;

namespace Palforge.Memory;

internal sealed unsafe class DirectMemory : IMemory
{
    private readonly RegionMap _regions;
    private readonly Lock _learnGate;

    private MemoryRegion[] _learned;

    public DirectMemory(RegionMap regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        _regions = regions;
        _learnGate = new Lock();
        _learned = [];
    }

    public bool IsReadable(nint address, int length)
    {
        return Readable(address, length);
    }

    public bool TryRead<T>(nint address, out T value)
        where T : unmanaged
    {
        if (!Readable(address, sizeof(T)))
        {
            value = default;
            return false;
        }

        value = Unsafe.ReadUnaligned<T>((void*)address);
        return true;
    }

    public bool TryRead(nint address, Span<byte> destination)
    {
        if (!Readable(address, destination.Length))
            return false;

        new ReadOnlySpan<byte>((void*)address, destination.Length).CopyTo(destination);
        return true;
    }

    public bool TryWrite<T>(nint address, in T value)
        where T : unmanaged
    {
        if (!IsWritable(address, sizeof(T)))
            return false;

        Unsafe.WriteUnaligned((void*)address, value);
        return true;
    }

    public bool TryWrite(nint address, ReadOnlySpan<byte> source)
    {
        if (!IsWritable(address, source.Length))
            return false;

        source.CopyTo(new Span<byte>((void*)address, source.Length));
        return true;
    }

    public bool WriteProtected<T>(nint address, in T value)
        where T : unmanaged
    {
        if (address is 0)
            return false;

        var size = (nuint)sizeof(T);

        if (!PInvoke.VirtualProtect((void*)address, size, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out var previous))
            return false;

        Unsafe.WriteUnaligned((void*)address, value);

        PInvoke.VirtualProtect((void*)address, size, previous, out _);

        return true;
    }

    public bool WriteProtected(nint address, ReadOnlySpan<byte> source)
    {
        if (address is 0)
            return false;

        if (source.IsEmpty)
            return true;

        var size = (nuint)source.Length;

        if (!PInvoke.VirtualProtect((void*)address, size, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out var previous))
            return false;

        source.CopyTo(new Span<byte>((void*)address, source.Length));

        PInvoke.VirtualProtect((void*)address, size, previous, out _);

        return true;
    }

    private bool Readable(nint address, int length)
    {
        if (_regions.IsReadable(address, length) || LearnedReadable(address, length))
            return true;

        if (RegionMap.TryLiveRegion(address, out var region) && region.IsReadable && region.Contains(address, length))
        {
            Learn(region);

            return true;
        }

        return RegionMap.IsCommittedReadable(address, length);
    }

    private bool LearnedReadable(nint address, int length)
    {
        var learned = _learned;

        var low = 0;
        var high = learned.Length - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var region = learned[middle];

            if (region.Contains(address))
                return region.IsReadable && region.Contains(address, length);

            if (address < region.Address)
                high = middle - 1;
            else
                low = middle + 1;
        }

        return false;
    }

    private void Learn(MemoryRegion region)
    {
        lock (_learnGate)
        {
            foreach (var existing in _learned)
            {
                if (existing.Address == region.Address)
                    return;
            }

            var grown = new MemoryRegion[_learned.Length + 1];

            Array.Copy(_learned, grown, _learned.Length);
            grown[^1] = region;
            Array.Sort(grown, static (left, right) => left.Address.CompareTo(right.Address));

            _learned = grown;
        }
    }

    private bool IsWritable(nint address, int length)
    {
        return _regions.TryGetRegion(address, out var region) && region.IsWritable && region.Contains(address, length);
    }
}