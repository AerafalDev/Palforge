using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Probes;

internal sealed class FunctionProbe : ProbeBase
{
    private const int FunctionScan = 0x200;
    private const int EnumScan = 0x200;
    private const int MaxParameters = 256;
    private const int MaxEnumMembers = 8192;
    private const int MinFunctions = 3;
    private const int MinEnums = 2;
    private const int FunctionSample = 4096;
    private const int EnumSample = 4096;
    private const ulong ParmFlag = 0x80;
    private const string Separator = "::";

    private readonly nint[] _functions;
    private readonly nint[] _enums;
    private readonly int _childProperties;
    private readonly int _fieldNext;
    private readonly int _elementSize;
    private readonly int _offsetInternal;
    private readonly int _propertyFlags;

    public FunctionProbe(IMemory memory, ObjectArrayView view, INameResolver names, LayoutTable known)
        : base(memory, view, names, known)
    {
        _childProperties = known.OffsetOrThrow(LayoutNames.ChildProperties);
        _fieldNext = known.OffsetOrThrow(LayoutNames.FieldChainNext);
        _elementSize = known.OffsetOrThrow(LayoutNames.ElementSize);
        _offsetInternal = known.OffsetOrThrow(LayoutNames.OffsetInternal);
        _propertyFlags = known.OffsetOrThrow(LayoutNames.PropertyFlags);

        _functions = [.. ObjectsOfClass("Function", FunctionSample)];
        _enums = [.. ObjectsOfClass("Enum", EnumSample)];
    }

    public LayoutTable Probe()
    {
        return new LayoutTable(
        [
            _functions.Length >= MinFunctions
                ? Member(LayoutNames.NumParms, SolveNumParms(), FunctionScan)
                : LayoutMember.NotAttempted(LayoutNames.NumParms, $"only {_functions.Length} functions were found"),
            _functions.Length >= MinFunctions
                ? Member(LayoutNames.ParmsSize, SolveParmsSize(), FunctionScan)
                : LayoutMember.NotAttempted(LayoutNames.ParmsSize, $"only {_functions.Length} functions were found"),
            _enums.Length >= MinEnums
                ? Member(LayoutNames.EnumNames, SolveEnumNames(), EnumScan)
                : LayoutMember.NotAttempted(LayoutNames.EnumNames, $"only {_enums.Length} enums were found")
        ]);
    }

    private Resolution<int> SolveNumParms()
    {
        var expected = _functions.ToDictionary(function => function, function => Parameters(function).Count);

        if (expected.Values.Distinct().Count() < 2)
            return Resolution<int>.NotFound(1);

        return SolveInteger(FunctionScan, expected);
    }

    private Resolution<int> SolveParmsSize()
    {
        var expected = _functions.ToDictionary(function => function, ParmsBlockSize);

        if (expected.Values.Distinct().Count() < 2)
            return Resolution<int>.NotFound(1);

        return SolveInteger(FunctionScan, expected);
    }

    private int ParmsBlockSize(nint function)
    {
        var end = 0;

        foreach (var parameter in Parameters(function))
        {
            if (Memory.TryRead(parameter + _offsetInternal, out int offset) && Memory.TryRead(parameter + _elementSize, out int size))
                end = Math.Max(end, offset + size);
        }

        return end;
    }

    private Resolution<int> SolveInteger(int scan, Dictionary<nint, int> expected)
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= scan; offset += sizeof(ushort))
        {
            if (expected.All(entry => Memory.TryRead(entry.Key + offset, out ushort value) && value == entry.Value))
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, expected.Count);
    }

    private Resolution<int> SolveEnumNames()
    {
        var candidates = new List<Candidate<int>>();

        for (var offset = 0; offset <= EnumScan; offset += nint.Size)
        {
            if (_enums.Count(address => NamesEveryMember(address, offset)) >= MinEnums)
                candidates.Add(new Candidate<int>(0, offset));
        }

        return Unanimity.EnsureOne(candidates, _enums.Length);
    }

    private bool NamesEveryMember(nint address, int offset)
    {
        if (!Memory.TryRead(address + offset, out nint pairs) || pairs is 0)
            return false;

        if (!Memory.TryRead(address + offset + nint.Size, out int count) || count is <= 0 or > MaxEnumMembers)
            return false;

        var prefix = NameOf(address) + Separator;
        var stride = sizeof(long) * 2;

        for (var index = 0; index < count; index++)
        {
            if (!Memory.TryRead(pairs + index * stride, out int id) || !Names.TryResolve(id, out var member))
                return false;

            if (!member.StartsWith(prefix, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private List<nint> Parameters(nint function)
    {
        var parameters = new List<nint>();

        if (!Memory.TryRead(function + _childProperties, out nint field))
            return parameters;

        var walked = 0;

        while (field is not 0 && walked <= MaxParameters)
        {
            if (Memory.TryRead(field + _propertyFlags, out ulong flags) && (flags & ParmFlag) is not 0)
                parameters.Add(field);

            walked++;

            if (!Memory.TryRead(field + _fieldNext, out field))
                return [];
        }

        return parameters;
    }
}