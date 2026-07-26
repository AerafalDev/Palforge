namespace Palforge.Unreal.Names;

internal sealed unsafe class NativeFNameNatives : IFNameNatives
{
    private readonly delegate* unmanaged[Cdecl]<nint, nint, int, void> _construct;
    private readonly delegate* unmanaged[Cdecl]<nint, nint, void> _toString;

    public NativeFNameNatives(nint construct, nint toString)
    {
        ArgumentOutOfRangeException.ThrowIfZero(toString);

        _construct = (delegate* unmanaged[Cdecl]<nint, nint, int, void>)construct;
        _toString = (delegate* unmanaged[Cdecl]<nint, nint, void>)toString;
    }

    public void Construct(nint self, nint wideName, int findType)
    {
        if (_construct is not null)
            _construct(self, wideName, findType);
    }

    public void ToString(nint self, nint outString)
    {
        _toString(self, outString);
    }
}