namespace Palforge.Unreal.Hooks;

internal sealed class HookEntry
{
    public int NameId { get; }

    public nint ClassFilter { get; }

    public bool Wide { get; }

    public HookCallback? Prefix { get; }

    public HookCallback? Postfix { get; }

    public HookEntry(int nameId, nint classFilter, bool wide, HookCallback? prefix, HookCallback? postfix)
    {
        NameId = nameId;
        ClassFilter = classFilter;
        Wide = wide;
        Prefix = prefix;
        Postfix = postfix;
    }
}