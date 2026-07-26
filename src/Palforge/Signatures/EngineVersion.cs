namespace Palforge.Signatures;

internal readonly record struct EngineVersion(ushort Major, ushort Minor)
{
    public bool IsPlausible =>
        Major switch
        {
            4 => Minor is >= 1 and <= 27,
            5 => Minor <= 15,
            _ => false
        };

    public override string ToString()
    {
        return $"UE{Major}.{Minor}";
    }
}