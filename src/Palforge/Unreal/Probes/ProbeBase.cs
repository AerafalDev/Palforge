using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Probes;

internal abstract class ProbeBase
{
    protected IMemory Memory { get; }

    protected INameResolver Names { get; }

    protected HashSet<nint> Addresses { get; }

    protected int NamePrivate { get; }

    protected int ClassPrivate { get; }

    protected ProbeBase(IMemory memory, ObjectArrayView view, INameResolver names, LayoutTable known)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(known);

        Memory = memory;
        Names = names;
        Addresses = [.. view.Addresses()];
        NamePrivate = known.OffsetOrThrow(LayoutNames.NamePrivate);
        ClassPrivate = known.OffsetOrThrow(LayoutNames.ClassPrivate);
    }

    protected string NameOf(nint address)
    {
        return Memory.TryRead(address + NamePrivate, out int id) && Names.TryResolve(id, out var name) ? name : string.Empty;
    }

    protected nint ClassOf(nint address)
    {
        return Memory.TryRead(address + ClassPrivate, out nint klass) && Addresses.Contains(klass) ? klass : 0;
    }

    protected IEnumerable<nint> ObjectsOfClass(string className, int limit = int.MaxValue)
    {
        if (limit <= 0 || !Names.TryFind(className, out var classNameId) || classNameId is 0)
            yield break;

        var found = 0;

        foreach (var address in Addresses)
        {
            var klass = ClassOf(address);

            if (klass is 0 || !Memory.TryRead(klass + NamePrivate, out int name) || name != classNameId)
                continue;

            yield return address;

            if (++found >= limit)
                yield break;
        }
    }

    protected static LayoutMember Member(string name, Resolution<int> resolution, int scan, string? diagnostic = null)
    {
        return LayoutMemberFactory.Member(name, resolution, scan, diagnostic);
    }

    protected static LayoutMember Undetermined<T>(string name, Resolution<T> resolution, int scan, string? diagnostic = null)
    {
        return LayoutMemberFactory.Undetermined(name, resolution, scan, diagnostic);
    }
}