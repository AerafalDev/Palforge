using System.Runtime.InteropServices;

namespace Palforge.Tests.Memory;

internal sealed unsafe partial class NativeScratch : IDisposable
{
    private const uint MemCommit = 0x1000;
    private const uint MemReserve = 0x2000;
    private const uint MemRelease = 0x8000;

    public const int PageSize = 0x1000;

    private void* _base;

    public int Pages { get; }

    public nint Address =>
        (nint)_base;

    public nint End =>
        Address + Pages * PageSize;

    public NativeScratch(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);

        _base = VirtualAlloc(null, (nuint)(pages * PageSize), MemCommit | MemReserve, PageProtection.ReadWrite);

        if (_base is null)
            throw new InvalidOperationException("VirtualAlloc failed.");

        Pages = pages;
    }

    public static nint ReserveWithoutCommitting(int pages)
    {
        var address = VirtualAlloc(null, (nuint)(pages * PageSize), MemReserve, PageProtection.NoAccess);

        return address is null
            ? throw new InvalidOperationException("VirtualAlloc failed.")
            : (nint)address;
    }

    public nint PageAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Pages);

        return Address + index * PageSize;
    }

    public void Protect(int page, uint protection)
    {
        if (!VirtualProtect((void*)PageAt(page), PageSize, protection, out _))
            throw new InvalidOperationException("VirtualProtect failed.");
    }

    public void Dispose()
    {
        if (_base is null)
            return;

        VirtualFree(_base, 0, MemRelease);

        _base = null;
    }

    [LibraryImport("kernel32", SetLastError = true)]
    private static partial void* VirtualAlloc(void* address, nuint size, uint allocationType, uint protection);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualFree(void* address, nuint size, uint freeType);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualProtect(void* address, nuint size, uint protection, out uint oldProtection);
}