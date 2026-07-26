using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Probes;

internal sealed class StructProbe : ProbeBase
{
    private const int StructScan = 0x200;
    private const int OwnerScan = 0x30;
    private const int StructSample = 4096;
    private const int RootSample = 256;
    private const int MinObservations = 4;
    private const int MinGrowth = 3;
    private const int MinDistinctSizes = 8;
    private const int MinChainLength = 2;
    private const int MinPropertiesSize = 8;
    private const int MaxPropertiesSize = 0x100000;
    private const int MaxChainLength = 4096;
    private const string DefaultPrefix = "Default__";
    private const string ChildPropertiesNote = "shares its head pointer with PropertyLink, so no offset separates them — tabled by design";

    private static readonly string[] s_rootChain = ["Class", "Struct", "Field", "Object"];

    private readonly nint[] _structs;
    private readonly nint[] _classes;
    private readonly int _outerPrivate;
    private readonly nint _classClass;
    private readonly nint _scriptStructClass;
    private readonly nint _functionClass;

    public StructProbe(IMemory memory, ObjectArrayView view, INameResolver names, LayoutTable objectBase) : base(memory, view, names, objectBase)
    {
        _outerPrivate = objectBase.OffsetOrThrow(LayoutNames.OuterPrivate);

        _classClass = FindMetaClass();
        _scriptStructClass = FindClassNamed("ScriptStruct");
        _functionClass = FindClassNamed("Function");

        _structs = SampleStructs(view);
        _classes = [.. _structs.Where(address => ClassOf(address) == _classClass)];
    }

    private nint[] SampleStructs(ObjectArrayView view)
    {
        var addresses = view.Addresses().ToArray();
        var sample = new List<nint>(StructSample);
        var seen = new HashSet<nint>();

        for (var index = 0; index < addresses.Length && sample.Count < RootSample; index++)
        {
            if (IsStruct(addresses[index]) && seen.Add(addresses[index]))
                sample.Add(addresses[index]);
        }

        var stride = Math.Max(1, addresses.Length / StructSample);

        for (var index = 0; index < addresses.Length && sample.Count < StructSample; index += stride)
        {
            if (IsStruct(addresses[index]) && seen.Add(addresses[index]))
                sample.Add(addresses[index]);
        }

        return [.. sample];
    }

    private nint FindMetaClass()
    {
        if (!Names.TryFind("Class", out var classId) || classId is 0)
            return 0;

        foreach (var address in Addresses)
        {
            if (Memory.TryRead(address + NamePrivate, out int id) && id == classId
                && Memory.TryRead(address + ClassPrivate, out nint klass) && klass == address)
                return address;
        }

        return 0;
    }

    private nint FindClassNamed(string name)
    {
        if (_classClass is 0 || !Names.TryFind(name, out var wanted) || wanted is 0)
            return 0;

        foreach (var address in Addresses)
        {
            if (Memory.TryRead(address + NamePrivate, out int id) && id == wanted
                && Memory.TryRead(address + ClassPrivate, out nint klass) && klass == _classClass)
                return address;
        }

        return 0;
    }

    public LayoutTable Probe()
    {
        var super = SolveSuperStruct();
        var chain = SolveChildren();
        var childProperties = SolveChildProperties();
        var defaultObject = SolveClassDefaultObject();

        var pointers = new HashSet<int> { ClassPrivate, _outerPrivate };

        foreach (var resolution in (Resolution<int>[])[super, childProperties, defaultObject])
        {
            if (resolution.TryGetValue(out var offset))
                pointers.Add(offset);
        }

        if (chain.TryGetValue(out var chained))
        {
            pointers.Add(chained.Children);
            pointers.Add(chained.Next);
        }

        var size = super.TryGetValue(out var superStruct) ? SolvePropertiesSize(superStruct, pointers) : Resolution<int>.NotFound(1);
        var alignment = size.TryGetValue(out var propertiesSize) ? SolveMinAlignment(propertiesSize) : Resolution<int>.NotFound(1);

        var diagnostic = chain.IsResolved && defaultObject.IsResolved
            ? string.Empty
            : $"[structs={_structs.Length} classes={_classes.Length} addr={Addresses.Count} super={(super.TryGetValue(out var ss) ? ss : -1):X} psize={(size.TryGetValue(out var ps) ? ps : -1):X}] {DumpStruct()}";

        return new LayoutTable(
        [
            Member(LayoutNames.SuperStruct, super, StructScan),
            super.IsResolved ? Member(LayoutNames.PropertiesSize, size, StructScan) : LayoutMember.NotAttempted(LayoutNames.PropertiesSize, "the super chain is unknown"),
            size.IsResolved ? Member(LayoutNames.MinAlignment, alignment, StructScan) : LayoutMember.NotAttempted(LayoutNames.MinAlignment, "the properties size is unknown"),
            Member(LayoutNames.ChildProperties, childProperties, StructScan, ChildPropertiesNote),
            chain.TryGetValue(out var links) ? LayoutMember.Derived(LayoutNames.Children, links.Children) : Undetermined(LayoutNames.Children, chain, StructScan, diagnostic),
            chain.TryGetValue(out var next) ? LayoutMember.Derived(LayoutNames.FieldNext, next.Next) : Undetermined(LayoutNames.FieldNext, chain, StructScan, diagnostic),
            Member(LayoutNames.ClassDefaultObject, defaultObject, StructScan, diagnostic)
        ]);
    }

    private Resolution<int> SolveSuperStruct()
    {
        var candidates = new List<Candidate<int>>();

        if (_classClass is 0)
            return Resolution<int>.NotFound(1);

        for (var offset = 0; offset <= StructScan; offset += nint.Size)
        {
            if (offset == ClassPrivate || offset == _outerPrivate)
                continue;

            if (ClimbsTheKnownChain(_classClass, offset))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private bool ClimbsTheKnownChain(nint start, int offset)
    {
        var current = start;

        for (var step = 1; step < s_rootChain.Length; step++)
        {
            if (!Memory.TryRead(current + offset, out nint super) || super is 0 || !Addresses.Contains(super))
                return false;

            if (NameOf(super) != s_rootChain[step])
                return false;

            current = super;
        }

        return Memory.TryRead(current + offset, out nint root) && root is 0;
    }

    private Resolution<int> SolvePropertiesSize(int superStruct, HashSet<int> pointers)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= StructScan; offset += sizeof(int))
        {
            if (pointers.Contains(offset))
                continue;

            var observed = 0;
            var growth = 0;
            var bad = 0;
            var distinct = new HashSet<int>();

            foreach (var address in _classes)
            {
                if (!Memory.TryRead(address + offset, out int size))
                    continue;

                if (size < MinPropertiesSize || size > MaxPropertiesSize || size % sizeof(int) is not 0)
                {
                    bad++;
                    continue;
                }

                observed++;
                distinct.Add(size);

                if (!Memory.TryRead(address + superStruct, out nint super) || super is 0 || !Memory.TryRead(super + offset, out int parent))
                    continue;

                if (parent > size)
                    bad++;
                else if (parent < size)
                    growth++;
            }

            if (observed >= MinObservations && growth >= MinGrowth && distinct.Count >= MinDistinctSizes && bad <= observed / 10)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private Resolution<int> SolveMinAlignment(int propertiesSize)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= StructScan; offset += sizeof(int))
        {
            if (offset == propertiesSize)
                continue;

            var observed = 0;
            var bad = 0;

            foreach (var address in _structs)
            {
                if (!Memory.TryRead(address + offset, out int alignment))
                    continue;

                if (IsAlignment(alignment))
                    observed++;
                else
                    bad++;
            }

            if (observed >= MinObservations && bad <= observed / 20)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private Resolution<int> SolveChildProperties()
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= StructScan; offset += nint.Size)
        {
            if (offset == ClassPrivate || offset == _outerPrivate)
                continue;

            var heads = new List<(nint Field, nint Owner)>();
            var empty = 0;

            foreach (var address in _structs)
            {
                if (!Memory.TryRead(address + offset, out nint field))
                    continue;

                if (field is 0)
                    empty++;
                else if (!Addresses.Contains(field) && LooksLikeField(field))
                    heads.Add((field, address));
            }

            if (heads.Count < MinObservations || empty < MinObservations)
                continue;

            var best = 0;

            for (var ownerOffset = 0; ownerOffset <= OwnerScan; ownerOffset += nint.Size)
            {
                var backReferences = heads.Count(head => Memory.TryRead(head.Field + ownerOffset, out nint owner) && owner == head.Owner);

                best = Math.Max(best, backReferences);
            }

            if (best >= heads.Count - heads.Count / 10)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private string DumpStruct()
    {
        var target = _classes.FirstOrDefault(address => NameOf(address) is "Object");

        if (target is 0)
            target = _structs.Length > 0 ? _structs[0] : 0;

        if (target is 0)
            return "no struct";

        var parts = new List<string>();

        for (var offset = 0x40; offset <= 0x120; offset += nint.Size)
        {
            if (!Memory.TryRead(target + offset, out nint value))
            {
                parts.Add($"{offset:X}=?");
                continue;
            }

            var kind = value is 0 ? "0"
                : Addresses.Contains(value) ? "O"
                : LooksLikeField(value) ? ReferencesOwner(value, target) ? "Fo" : "F"
                : Memory.IsReadable(value, nint.Size) ? "r"
                : ".";

            parts.Add($"{offset:X}={kind}");
        }

        return $"{NameOf(target)}: {string.Join(' ', parts)}";
    }

    private bool ReferencesOwner(nint field, nint owner)
    {
        for (var offset = 0; offset <= OwnerScan; offset += nint.Size)
        {
            if (Memory.TryRead(field + offset, out nint pointer) && pointer == owner)
                return true;
        }

        return false;
    }

    private Resolution<FieldChain> SolveChildren()
    {
        var candidates = new List<Candidate<FieldChain>>();

        for (var children = 0; children <= StructScan; children += nint.Size)
        {
            if (children == ClassPrivate || children == _outerPrivate)
                continue;

            for (var next = 0; next <= StructScan; next += nint.Size)
            {
                if (next == ClassPrivate || next == _outerPrivate || next == children)
                    continue;

                if (ChainStaysWithinTheOwner(children, next))
                    candidates.Add(new Candidate<FieldChain>(0, new FieldChain(children, next)));
            }
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private bool ChainStaysWithinTheOwner(int children, int next)
    {
        var owners = 0;
        var longest = 0;

        foreach (var address in _structs)
        {
            if (!Memory.TryRead(address + children, out nint element) || element is 0)
                continue;

            var seen = new HashSet<nint>();
            var clean = true;

            while (element is not 0)
            {
                if (!Addresses.Contains(element) || !seen.Add(element) || seen.Count > MaxChainLength)
                {
                    clean = false;
                    break;
                }

                if (!Memory.TryRead(element + _outerPrivate, out nint owner) || owner != address)
                {
                    clean = false;
                    break;
                }

                if (!Memory.TryRead(element + next, out element))
                {
                    clean = false;
                    break;
                }
            }

            if (!clean)
                continue;

            owners++;
            longest = Math.Max(longest, seen.Count);
        }

        return owners >= MinObservations / 2 && longest >= MinChainLength;
    }

    private Resolution<int> SolveClassDefaultObject()
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= StructScan; offset += nint.Size)
        {
            if (offset == ClassPrivate || offset == _outerPrivate)
                continue;

            var matched = 0;
            var bad = 0;

            foreach (var address in _classes)
            {
                if (!Memory.TryRead(address + offset, out nint candidate) || candidate is 0)
                    continue;

                if (!Addresses.Contains(candidate) || !IsDefaultObjectOf(candidate, address))
                {
                    bad++;
                    continue;
                }

                matched++;
            }

            if (matched >= MinObservations && bad <= matched / 10)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private bool IsDefaultObjectOf(nint candidate, nint owner)
    {
        var name = NameOf(candidate);

        return name.StartsWith(DefaultPrefix, StringComparison.Ordinal) && name[DefaultPrefix.Length..] == NameOf(owner);
    }


    private bool LooksLikeField(nint address)
    {
        return Memory.IsReadable(address, nint.Size) && Memory.TryRead(address, out nint vtable) && vtable is not 0;
    }

    private bool IsStruct(nint address)
    {
        var klass = ClassOf(address);

        if (klass is 0 || (klass != _classClass && klass != _scriptStructClass && klass != _functionClass))
            return false;

        return !NameOf(address).StartsWith(DefaultPrefix, StringComparison.Ordinal);
    }

    private static bool IsAlignment(int value)
    {
        return value is > 0 and <= 16 && (value & (value - 1)) is 0;
    }
}