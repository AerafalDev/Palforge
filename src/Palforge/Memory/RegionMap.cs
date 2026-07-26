using Windows.Win32;
using Windows.Win32.System.Memory;
using Palforge.Extensions;

namespace Palforge.Memory;

internal sealed class RegionMap
{
    private readonly MemoryRegion[] _regions;

    public RegionMap(IEnumerable<MemoryRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        _regions = [.. regions.OrderBy(static region => region.Address)];
    }

    public static RegionMap FromCurrentProcess()
    {
        return new RegionMap(Query());
    }

    public bool TryGetRegion(nint address, out MemoryRegion region)
    {
        var low = 0;
        var high = _regions.Length - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            if (_regions[middle].Contains(address))
            {
                region = _regions[middle];
                return true;
            }

            if (address < _regions[middle].Address)
                high = middle - 1;
            else
                low = middle + 1;
        }

        region = default;
        return false;
    }

    public bool IsReadable(nint address, int length)
    {
        if (address is 0 || length < 0)
            return false;

        if (length is 0)
            return true;

        var end = address + length;

        while (address < end)
        {
            if (!TryGetRegion(address, out var region) || !region.IsReadable)
                return false;

            address = region.End;
        }

        return true;
    }

    public static unsafe bool IsCommittedReadable(nint address, int length)
    {
        if (address is 0 || length < 0)
            return false;

        if (length is 0)
            return true;

        var information = default(MEMORY_BASIC_INFORMATION);
        var size = (nuint)sizeof(MEMORY_BASIC_INFORMATION);
        var end = address + length;

        while (address < end)
        {
            if (PInvoke.VirtualQuery((void*)address, &information, size) != size)
                return false;

            if (information.State is not VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT || !AccessOf(information.Protect).HasFlag(MemoryAccess.Read))
                return false;

            var next = (nint)information.BaseAddress + (nint)information.RegionSize;

            if (next <= address)
                return false;

            address = next;
        }

        return true;
    }

    public static unsafe bool TryLiveRegion(nint address, out MemoryRegion region)
    {
        region = default;

        if (address is 0)
            return false;

        var information = default(MEMORY_BASIC_INFORMATION);
        var size = (nuint)sizeof(MEMORY_BASIC_INFORMATION);

        if (PInvoke.VirtualQuery((void*)address, &information, size) != size || information.State is not VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT)
            return false;

        var access = AccessOf(information.Protect);

        if (access is MemoryAccess.None)
            return false;

        region = new MemoryRegion((nint)information.BaseAddress, information.RegionSize, access);

        return true;
    }

    private static unsafe List<MemoryRegion> Query()
    {
        var regions = new List<MemoryRegion>(512);
        var information = default(MEMORY_BASIC_INFORMATION);

        var size = (nuint)sizeof(MEMORY_BASIC_INFORMATION);
        nint address = 0;

        while (PInvoke.VirtualQuery((void*)address, &information, size) == size)
        {
            var next = (nint)information.BaseAddress + (nint)information.RegionSize;

            if (next <= address)
                break;

            if (information.State is VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT)
            {
                var access = AccessOf(information.Protect);

                if (access is not MemoryAccess.None)
                    regions.Add(new MemoryRegion((nint)information.BaseAddress, information.RegionSize, access));
            }

            address = next;
        }

        return regions;
    }

    private static MemoryAccess AccessOf(PAGE_PROTECTION_FLAGS protection)
    {
        if (protection.HasFlag(PAGE_PROTECTION_FLAGS.PAGE_GUARD))
            return MemoryAccess.None;

        var access = MemoryAccess.None;

        if (protection.HasAnyFlag(
                PAGE_PROTECTION_FLAGS.PAGE_READONLY,
                PAGE_PROTECTION_FLAGS.PAGE_READWRITE,
                PAGE_PROTECTION_FLAGS.PAGE_WRITECOPY,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READ,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_WRITECOPY))
            access |= MemoryAccess.Read;

        if (protection.HasAnyFlag(
                PAGE_PROTECTION_FLAGS.PAGE_READWRITE,
                PAGE_PROTECTION_FLAGS.PAGE_WRITECOPY,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_WRITECOPY))
            access |= MemoryAccess.Write;

        if (protection.HasAnyFlag(
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READ,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE,
                PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_WRITECOPY))
            access |= MemoryAccess.Execute;

        return access;
    }
}