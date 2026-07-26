using Palforge.Layout;
using Palforge.Signatures;

namespace Palforge.Unreal.Probes;

internal static class LayoutMemberFactory
{
    public static LayoutMember Member(string name, Resolution<int> resolution, int scan, string? diagnostic = null)
    {
        return resolution.TryGetValue(out var offset)
            ? LayoutMember.Derived(name, offset)
            : Undetermined(name, resolution, scan, diagnostic);
    }

    public static LayoutMember Undetermined<T>(string name, Resolution<T> resolution, int scan, string? diagnostic = null)
    {
        return LayoutMember.Undetermined(name, resolution.Agreeing, $"{resolution} (searched to 0x{scan:X}) {diagnostic}".TrimEnd());
    }
}