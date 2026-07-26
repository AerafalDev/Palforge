using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Probes;

internal sealed class PropertyProbe : ProbeBase
{
    private const int FieldScan = 0x100;
    private const int FieldNextScan = 0x40;
    private const int FieldNameScan = 0x20;
    private const int MaxChainLength = 256;
    private const int HeadSample = 512;
    private const int ParameterSample = 512;
    private const int PropertyFlagsScan = 0x78;
    private const int MinObservations = 4;
    private const ulong ParmFlag = 0x80; // EPropertyFlags::CPF_Parm
    private const string PropertySuffix = "Property";

    private static readonly PropertyAnchor[] s_anchors =
    [
        new("Vector", ["X", "Y", "Z"], [0, 8, 16], 8),
        new("Guid", ["A", "B", "C", "D"], [0, 4, 8, 12], 4)
    ];

    private readonly Dictionary<string, nint> _heads;
    private readonly Dictionary<string, nint> _structs;
    private readonly List<nint> _probeHeads;
    private readonly List<nint> _parameters;
    private readonly int _childProperties;

    public PropertyProbe(IMemory memory, ObjectArrayView view, INameResolver names, LayoutTable known) : base(memory, view, names, known)
    {
        _heads = new Dictionary<string, nint>(StringComparer.Ordinal);
        _structs = new Dictionary<string, nint>(StringComparer.Ordinal);
        _probeHeads = [];
        _parameters = [];
        _childProperties = known.OffsetOrThrow(LayoutNames.ChildProperties);

        foreach (var anchor in s_anchors.Select(static anchor => anchor.Struct).Distinct())
        {
            if (!Names.TryFind(anchor, out var id) || id is 0)
                continue;

            foreach (var address in Addresses)
            {
                if (Memory.TryRead(address + NamePrivate, out int name) && name == id
                    && Memory.TryRead(address + _childProperties, out nint head) && head is not 0)
                {
                    _heads[anchor] = head;
                    _structs[anchor] = address;
                    break;
                }
            }
        }

        CollectHeads("ScriptStruct", HeadSample, _probeHeads);
        CollectHeads("Class", HeadSample, _probeHeads);
        CollectHeads("Function", ParameterSample, _parameters);
    }

    private void CollectHeads(string className, int cap, List<nint> into)
    {
        foreach (var address in ObjectsOfClass(className))
        {
            if (into.Count >= cap)
                break;

            if (Memory.TryRead(address + _childProperties, out nint head) && head is not 0)
                into.Add(head);
        }
    }

    public LayoutTable Probe()
    {
        var chain = SolveChain();

        if (!chain.TryGetValue(out var links))
        {
            return new LayoutTable(
            [
                Undetermined(LayoutNames.FieldChainNext, chain, FieldScan),
                Undetermined(LayoutNames.FieldNamePrivate, chain, FieldScan),
                LayoutMember.NotAttempted(LayoutNames.FieldOwner, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.FieldClassPrivate, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.FieldClassName, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.ArrayDim, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.ElementSize, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.OffsetInternal, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.PropertyFlags, "the property chain is unknown"),
                LayoutMember.NotAttempted(LayoutNames.PropertyBaseSize, "the property chain is unknown")
            ]);
        }

        var owner = SolveOwner(links.Next);
        var fieldClass = SolveFieldClass(links.Next, owner);
        var fieldClassName = fieldClass.TryGetValue(out var classOffset)
            ? SolveFieldClassName(links.Next, classOffset)
            : Resolution<int>.NotFound(1);

        return new LayoutTable(
        [
            LayoutMember.Derived(LayoutNames.FieldChainNext, links.Next),
            LayoutMember.Derived(LayoutNames.FieldNamePrivate, links.NamePrivate),
            Member(LayoutNames.FieldOwner, owner, FieldScan),
            Member(LayoutNames.FieldClassPrivate, fieldClass, FieldScan),
            fieldClass.IsResolved
                ? Member(LayoutNames.FieldClassName, fieldClassName, FieldScan)
                : LayoutMember.NotAttempted(LayoutNames.FieldClassName, "the field class pointer is unknown"),
            Member(LayoutNames.ArrayDim, SolveArrayDim(links.Next), FieldScan),
            Member(LayoutNames.ElementSize, SolveConstant(links.Next, static anchor => anchor.ElementSize), FieldScan),
            Member(LayoutNames.OffsetInternal, SolveOffsets(links.Next), FieldScan),
            Member(LayoutNames.PropertyFlags, SolvePropertyFlags(links.Next), FieldScan),
            fieldClassName.TryGetValue(out var nameOffset)
                ? Member(LayoutNames.PropertyBaseSize, SolveBaseSize(links.Next, classOffset, nameOffset), FieldScan)
                : LayoutMember.NotAttempted(LayoutNames.PropertyBaseSize, "field class names are unknown, so subclasses cannot be told apart")
        ]);
    }

    private Resolution<PropertyChain> SolveChain()
    {
        var candidates = new List<Candidate<PropertyChain>>();

        for (var next = 0; next <= FieldNextScan; next += nint.Size)
        {
            for (var name = 0; name <= FieldScan; name += sizeof(int))
            {
                if (NamesEveryAnchorMember(next, name))
                    candidates.Add(new Candidate<PropertyChain>(0, new PropertyChain(next, name)));
            }
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private bool NamesEveryAnchorMember(int next, int name)
    {
        foreach (var anchor in s_anchors)
        {
            var walked = Walk(anchor.Struct, next);

            if (walked.Count != anchor.Members.Length)
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var field in walked)
            {
                if (!Memory.TryRead(field + name, out int id) || !Names.TryResolve(id, out var resolved))
                    return false;

                seen.Add(resolved);
            }

            if (!seen.SetEquals(anchor.Members))
                return false;
        }

        return true;
    }

    private Resolution<int> SolveOwner(int next)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldScan; offset += nint.Size)
        {
            if (s_anchors.All(anchor => PointsAtItsStruct(anchor, next, offset)))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private bool PointsAtItsStruct(PropertyAnchor anchor, int next, int offset)
    {
        if (!_structs.TryGetValue(anchor.Struct, out var owner))
            return false;

        foreach (var field in Walk(anchor.Struct, next))
        {
            if (!Memory.TryRead(field + offset, out nint value) || value != owner)
                return false;
        }

        return true;
    }

    private Resolution<int> SolveFieldClass(int next, Resolution<int> owner)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldScan; offset += nint.Size)
        {
            if (owner.TryGetValue(out var ownerOffset) && offset == ownerOffset)
                continue;

            var shared = new List<nint>();
            var usable = true;

            foreach (var anchor in s_anchors)
            {
                if (!TryReadSharedPointer(anchor, next, offset, out var pointer) || Addresses.Contains(pointer))
                {
                    usable = false;
                    break;
                }

                shared.Add(pointer);
            }

            if (usable && shared.Distinct().Count() == shared.Count && LeadsToFieldClassName(next, offset))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private bool LeadsToFieldClassName(int next, int classPointer)
    {
        foreach (var anchor in s_anchors)
        {
            var named = false;

            for (var offset = 0; offset <= FieldNameScan; offset += sizeof(int))
            {
                if (FieldClassIsNamed(anchor, next, classPointer, offset))
                {
                    named = true;
                    break;
                }
            }

            if (!named)
                return false;
        }

        return true;
    }

    private bool TryReadSharedPointer(PropertyAnchor anchor, int next, int offset, out nint pointer)
    {
        pointer = 0;

        foreach (var field in Walk(anchor.Struct, next))
        {
            if (!Memory.TryRead(field + offset, out nint value) || value is 0 || !Memory.IsReadable(value, nint.Size))
                return false;

            if (pointer is 0)
                pointer = value;
            else if (pointer != value)
                return false;
        }

        return pointer is not 0;
    }

    private Resolution<int> SolveFieldClassName(int next, int classPointer)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldNameScan; offset += sizeof(int))
        {
            if (s_anchors.All(anchor => FieldClassIsNamed(anchor, next, classPointer, offset)))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private bool FieldClassIsNamed(PropertyAnchor anchor, int next, int classPointer, int offset)
    {
        foreach (var field in Walk(anchor.Struct, next))
        {
            if (!Memory.TryRead(field + classPointer, out nint fieldClass))
                return false;

            if (!Memory.TryRead(fieldClass + offset, out int id) || !Names.TryResolve(id, out var name)
                || !name.EndsWith(PropertySuffix, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private Resolution<int> SolveArrayDim(int next)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldScan; offset += sizeof(int))
        {
            if (s_anchors.All(anchor => Walk(anchor.Struct, next).All(field =>
                    Memory.TryRead(field + offset, out int dim) && dim is 1
                    && Memory.TryRead(field + offset + sizeof(int), out int size) && size == anchor.ElementSize)))
            {
                candidates.Add(new Candidate<int>(0, offset));
            }
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private Resolution<int> SolveConstant(int next, Func<PropertyAnchor, int> expected)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldScan; offset += sizeof(int))
        {
            if (s_anchors.All(anchor => Walk(anchor.Struct, next).All(field =>
                    Memory.TryRead(field + offset, out int value) && value == expected(anchor))))
            {
                candidates.Add(new Candidate<int>(0, offset));
            }
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private Resolution<int> SolveOffsets(int next)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= FieldScan; offset += sizeof(int))
        {
            if (s_anchors.All(anchor => MatchesOffsets(anchor, next, offset)))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, s_anchors.Length);
    }

    private bool MatchesOffsets(PropertyAnchor anchor, int next, int offset)
    {
        var seen = new List<int>();

        foreach (var field in Walk(anchor.Struct, next))
        {
            if (!Memory.TryRead(field + offset, out int value))
                return false;

            seen.Add(value);
        }

        seen.Sort();

        return seen.SequenceEqual(anchor.Offsets);
    }

    private Resolution<int> SolvePropertyFlags(int next)
    {
        if (_parameters.Count < MinObservations)
            return Resolution<int>.NotFound(1);

        var members = s_anchors.SelectMany(anchor => Walk(anchor.Struct, next)).ToList();

        if (members.Count < MinObservations)
            return Resolution<int>.NotFound(1);

        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= PropertyFlagsScan; offset += nint.Size)
        {
            var flaggedParameters = _parameters.Count(field => Memory.TryRead(field + offset, out ulong flags) && (flags & ParmFlag) is not 0);
            var flaggedMembers = members.Count(field => Memory.TryRead(field + offset, out ulong flags) && (flags & ParmFlag) is not 0);

            if (flaggedParameters >= _parameters.Count - (_parameters.Count / 5) && flaggedMembers is 0)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private Resolution<int> SolveBaseSize(int next, int classPointer, int classNameOffset)
    {
        var candidates = new List<Candidate<int>>();

        var shapes = new (string FieldClass, string TargetClass)[]
        {
            ("StructProperty", "ScriptStruct"),
            ("ObjectProperty", "Class")
        };

        var kinds = new List<(int Id, string TargetClass)>();

        foreach (var shape in shapes)
        {
            if (Names.TryFind(shape.FieldClass, out var id) && id is not 0)
                kinds.Add((id, shape.TargetClass));
        }

        var subclassed = _probeHeads
            .SelectMany(head => WalkFrom(head, next))
            .Select(field => (Field: field, Kind: FieldClassIdOf(field, classPointer, classNameOffset)))
            .Where(entry => kinds.Any(kind => kind.Id == entry.Kind))
            .ToArray();

        if (subclassed.Length < shapes.Length)
            return Resolution<int>.NotFound(shapes.Length);

        for (var offset = 0; offset <= FieldScan; offset += nint.Size)
        {
            var matched = 0;
            var usable = true;

            foreach (var (field, kind) in subclassed)
            {
                var expected = kinds.First(entry => entry.Id == kind).TargetClass;

                if (!Memory.TryRead(field + offset, out nint target) || !Addresses.Contains(target))
                {
                    usable = false;
                    break;
                }

                if (NameOf(ClassOf(target)) != expected)
                {
                    usable = false;
                    break;
                }

                matched++;
            }

            if (usable && matched == subclassed.Length)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, shapes.Length);
    }

    private int FieldClassIdOf(nint field, int classPointer, int classNameOffset)
    {
        if (!Memory.TryRead(field + classPointer, out nint fieldClass))
            return 0;

        return Memory.TryRead(fieldClass + classNameOffset, out int id) ? id : 0;
    }

    private List<nint> Walk(string owner, int next)
    {
        return _heads.TryGetValue(owner, out var head) ? WalkFrom(head, next) : [];
    }

    private List<nint> WalkFrom(nint field, int next)
    {
        var walked = new List<nint>();

        while (field is not 0 && walked.Count <= MaxChainLength)
        {
            walked.Add(field);

            if (!Memory.TryRead(field + next, out field))
                return [];
        }

        return walked;
    }
}