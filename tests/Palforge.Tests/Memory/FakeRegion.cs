using Palforge.Memory;

namespace Palforge.Tests.Memory;

internal sealed class FakeRegion
{
    public nint Address { get; }

    public byte[] Data { get; }

    public MemoryAccess Access { get; }

    public nint End =>
        Address + Data.Length;

    public FakeRegion(nint address, byte[] data, MemoryAccess access)
    {
        Address = address;
        Data = data;
        Access = access;
    }

    public bool Contains(nint address, int length)
    {
        return length >= 0 && address >= Address && address + length <= End;
    }

    public int OffsetOf(nint address)
    {
        return (int)(address - Address);
    }
}