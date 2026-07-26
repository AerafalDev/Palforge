namespace Palforge.Memory;

internal readonly record struct MemoryRegion(nint Address, nuint Size, MemoryAccess Access)
{
    public nint End =>
        Address + (nint)Size;

    public bool IsReadable =>
        Access.HasFlag(MemoryAccess.Read);

    public bool IsWritable =>
        Access.HasFlag(MemoryAccess.Write);

    public bool Contains(nint address)
    {
        return address >= Address && address < End;
    }

    public bool Contains(nint address, int length)
    {
        return address >= Address && length >= 0 && address + length <= End;
    }
}