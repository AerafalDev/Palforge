using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Probes;

internal sealed class ObjectBaseProbe
{
    private const int HeaderScan = 0x80;
    private const int SampleSize = 4096;
    private const int RootSample = 256;
    private const int MinKnownNames = 3;
    private const uint ClassDefaultObjectFlag = 0x10;
    private const string ClassName = "Class";
    private const string DefaultPrefix = "Default__";

    private static readonly string[] s_knownNames = ["Class", "Object", "Field", "Struct", "Package", "Function", "Enum", "ByteProperty", "IntProperty", "BoolProperty", "FloatProperty", "ObjectProperty"];

    private readonly IMemory _memory;
    private readonly INameResolver _names;
    private readonly HashSet<nint> _addresses;
    private readonly nint[] _sample;

    public ObjectBaseProbe(IMemory memory, ObjectArrayView view, INameResolver names)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(names);

        _memory = memory;
        _names = names;
        _addresses = [.. view.Addresses()];
        _sample = SampleObjects(view);
    }

    private static nint[] SampleObjects(ObjectArrayView view)
    {
        var addresses = view.Addresses().ToArray();

        if (addresses.Length <= SampleSize)
            return addresses;

        var sample = new List<nint>(SampleSize);

        for (var index = 0; index < RootSample; index++)
            sample.Add(addresses[index]);

        var stride = Math.Max(1, addresses.Length / (SampleSize - RootSample));

        for (var index = RootSample; index < addresses.Length && sample.Count < SampleSize; index += stride)
            sample.Add(addresses[index]);

        return [.. sample];
    }

    public LayoutTable Probe()
    {
        var klass = SolveClassPrivate();

        if (!klass.TryGetValue(out var classPrivate))
        {
            return new LayoutTable(
            [
                LayoutMember.Undetermined(LayoutNames.ClassPrivate, klass.Agreeing, $"{klass}; {DumpOffsets()}"),
                LayoutMember.NotAttempted(LayoutNames.NamePrivate, "the class pointer is unknown"),
                LayoutMember.NotAttempted(LayoutNames.OuterPrivate, "the class pointer is unknown"),
                LayoutMember.NotAttempted(LayoutNames.ObjectFlags, "the class pointer is unknown")
            ]);
        }

        var name = SolveNamePrivate(classPrivate.Offset, classPrivate.ClassObject);

        if (!name.TryGetValue(out var namePrivate))
        {
            return new LayoutTable(
            [
                LayoutMember.Derived(LayoutNames.ClassPrivate, classPrivate.Offset),
                LayoutMember.Undetermined(LayoutNames.NamePrivate, name.Agreeing, $"{name}; {DumpOffsets()}"),
                LayoutMemberFactory.Member(LayoutNames.OuterPrivate, SolveOuterPrivate(classPrivate.Offset), HeaderScan),
                LayoutMember.NotAttempted(LayoutNames.ObjectFlags, "object names are unknown, so default objects cannot be told apart")
            ]);
        }

        var outer = SolveOuterPrivate(classPrivate.Offset);
        var flags = SolveObjectFlags(namePrivate);

        return new LayoutTable(
        [
            LayoutMember.Derived(LayoutNames.ClassPrivate, classPrivate.Offset),
            LayoutMember.Derived(LayoutNames.NamePrivate, namePrivate),
            outer.TryGetValue(out var outerPrivate)
                ? LayoutMember.Derived(LayoutNames.OuterPrivate, outerPrivate)
                : LayoutMemberFactory.Undetermined(LayoutNames.OuterPrivate, outer, HeaderScan),
            flags.TryGetValue(out var objectFlags)
                ? LayoutMember.Derived(LayoutNames.ObjectFlags, objectFlags)
                : LayoutMemberFactory.Undetermined(LayoutNames.ObjectFlags, flags, HeaderScan)
        ]);
    }

    private Resolution<ClassPointer> SolveClassPrivate()
    {
        var candidates = new List<Candidate<ClassPointer>>();

        for (var offset = 0; offset <= HeaderScan; offset += nint.Size)
        {
            if (!MostlyPointsAtAnObject(offset))
                continue;

            var fixedPoints = _addresses.Where(address => PointsAtItself(address, offset)).ToArray();

            if (fixedPoints.Length is 1)
                candidates.Add(new Candidate<ClassPointer>(0, new ClassPointer(offset, fixedPoints[0])));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private bool MostlyPointsAtAnObject(int offset)
    {
        var pointing = 0;

        foreach (var address in _sample)
        {
            if (_memory.TryRead(address + offset, out nint value) && value is not 0 && _addresses.Contains(value))
                pointing++;
        }

        return pointing >= _sample.Length - (_sample.Length / 100);
    }

    private string DumpOffsets()
    {
        var parts = new List<string>();

        for (var offset = 0; offset <= HeaderScan; offset += nint.Size)
        {
            var pointing = _sample.Count(address => _memory.TryRead(address + offset, out nint value) && value is not 0 && _addresses.Contains(value));
            var fixedPoints = _sample.Count(address => PointsAtItself(address, offset));

            if (pointing > 0)
                parts.Add($"+{offset:X}:{pointing}/{_sample.Length}obj,{fixedPoints}fix");
        }

        return $"sample={_sample.Length} total={_addresses.Count} [{string.Join(' ', parts)}]";
    }

    private Resolution<int> SolveNamePrivate(int classPrivate, nint classObject)
    {
        if (!_names.TryFind(ClassName, out var classId) || classId is 0)
            return Resolution<int>.NotFound(1);

        var known = new HashSet<int>();

        foreach (var name in s_knownNames)
        {
            if (_names.TryFind(name, out var id) && id is not 0)
                known.Add(id);
        }

        if (known.Count < MinKnownNames)
            return Resolution<int>.NotFound(1);

        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= HeaderScan; offset += sizeof(int))
        {
            if (offset == classPrivate)
                continue;

            if (!_memory.TryRead(classObject + offset, out int value) || value != classId)
                continue;

            var hits = new HashSet<int>();

            foreach (var address in _addresses)
            {
                if (_memory.TryRead(address + offset, out int name) && known.Contains(name))
                    hits.Add(name);
            }

            if (hits.Count >= MinKnownNames)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private Resolution<int> SolveOuterPrivate(int classPrivate)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= HeaderScan; offset += nint.Size)
        {
            if (offset == classPrivate)
                continue;

            var nulls = 0;
            var objects = 0;
            var strangers = 0;

            foreach (var address in _sample)
            {
                if (!_memory.TryRead(address + offset, out nint value))
                {
                    strangers++;
                    continue;
                }

                if (value is 0)
                    nulls++;
                else if (_addresses.Contains(value))
                    objects++;
                else
                    strangers++;
            }

            if (strangers is 0 && nulls > 0 && objects > nulls)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private Resolution<int> SolveObjectFlags(int namePrivate)
    {
        var candidates = new List<Candidate<int>>();

        var defaults = new List<nint>();
        var ordinary = new List<nint>();

        foreach (var address in _sample)
        {
            if (!_memory.TryRead(address + namePrivate, out int id) || !_names.TryResolve(id, out var name))
                continue;

            if (name.StartsWith(DefaultPrefix, StringComparison.Ordinal))
                defaults.Add(address);
            else
                ordinary.Add(address);
        }

        if (defaults.Count < MinKnownNames || ordinary.Count < MinKnownNames)
            return Resolution<int>.NotFound(1);

        for (var offset = 0; offset <= HeaderScan; offset += sizeof(int))
        {
            if (offset == namePrivate)
                continue;

            if (SeparatesByDefaultObjectFlag(offset, defaults, ordinary))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private bool SeparatesByDefaultObjectFlag(int offset, List<nint> defaults, List<nint> ordinary)
    {
        var flagged = defaults.Count(address => _memory.TryRead(address + offset, out uint value) && (value & ClassDefaultObjectFlag) is not 0);
        var leaked = ordinary.Count(address => _memory.TryRead(address + offset, out uint value) && (value & ClassDefaultObjectFlag) is not 0);

        return flagged >= defaults.Count - (defaults.Count / 10) && leaked <= ordinary.Count / 10;
    }

    private bool PointsAtItself(nint address, int offset)
    {
        return _memory.TryRead(address + offset, out nint value) && value == address;
    }
}