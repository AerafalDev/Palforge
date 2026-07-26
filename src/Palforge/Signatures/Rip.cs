using Palforge.Memory;

namespace Palforge.Signatures;

internal static class Rip
{
    private const int DisplacementSize = 4;

    public static bool TryResolve(IMemory memory, nint displacementAddress, out nint target)
    {
        target = 0;

        if (!memory.TryRead(displacementAddress, out int displacement))
            return false;

        target = displacementAddress + DisplacementSize + displacement;

        return true;
    }
}