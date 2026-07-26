namespace Palforge.Unreal.Names;

internal interface IFNameNatives
{
    void Construct(nint self, nint wideName, int findType);

    void ToString(nint self, nint outString);
}