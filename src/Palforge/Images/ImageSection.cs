namespace Palforge.Images;

internal readonly record struct ImageSection(string Name, nint Address, int Size, bool IsExecutable, bool IsReadable)
{
    public nint End =>
        Address + Size;
}