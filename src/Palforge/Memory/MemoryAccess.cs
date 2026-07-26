namespace Palforge.Memory;

[Flags]
internal enum MemoryAccess
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Execute = 1 << 2
}