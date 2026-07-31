using System.Runtime.InteropServices;
using Palforge.Memory;

namespace Palforge.Tests.Memory;

internal sealed class FakeMemory : IMemory
{
    private const int Alignment = 0x1000;

    private static readonly nint s_origin = unchecked((nint)0x0000_7FF0_0000_0000);

    private readonly List<FakeRegion> _regions = [];

    private nint _next = s_origin;

    public IReadOnlyList<MemoryRegion> Regions =>
        [.. _regions.Select(static region => new MemoryRegion(region.Address, (nuint)region.Data.Length, region.Access))];

    public nint Allocate(int size, MemoryAccess access = MemoryAccess.Read | MemoryAccess.Write)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var address = _next;

        _regions.Add(new FakeRegion(address, new byte[size], access));
        _next += (size + Alignment - 1) / Alignment * Alignment + Alignment;

        return address;
    }

    public nint AllocateAt(nint address, int size, MemoryAccess access = MemoryAccess.Read | MemoryAccess.Write)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        _regions.Add(new FakeRegion(address, new byte[size], access));

        return address;
    }

    public bool IsReadable(nint address, int length)
    {
        if (address is 0 || length < 0)
            return false;

        if (length is 0)
            return true;

        return TryGetRegion(address, length, MemoryAccess.Read, out _);
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

        if (!TryGetRegion(address, destination.Length, MemoryAccess.Read, out var region))
            return false;

        region.Data.AsSpan(region.OffsetOf(address), destination.Length).CopyTo(destination);

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

        if (!TryGetRegion(address, source.Length, MemoryAccess.Write, out var region))
            return false;

        source.CopyTo(region.Data.AsSpan(region.OffsetOf(address), source.Length));

        return true;
    }

    public bool WriteProtected<T>(nint address, in T value)
        where T : unmanaged
    {
        var local = value;
        var bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in local));

        if (!TryGetRegion(address, bytes.Length, MemoryAccess.Read, out var region))
            return false;

        bytes.CopyTo(region.Data.AsSpan(region.OffsetOf(address), bytes.Length));

        return true;
    }

    public bool WriteProtected(nint address, ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
            return true;

        if (!TryGetRegion(address, source.Length, MemoryAccess.Read, out var region))
            return false;

        source.CopyTo(region.Data.AsSpan(region.OffsetOf(address), source.Length));

        return true;
    }

    private bool TryGetRegion(nint address, int length, MemoryAccess access, out FakeRegion region)
    {
        foreach (var candidate in _regions)
        {
            if (!candidate.Contains(address, length))
                continue;

            if ((candidate.Access & access) != access)
                break;

            region = candidate;
            return true;
        }

        region = null!;
        return false;
    }
}