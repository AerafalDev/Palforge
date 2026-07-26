using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Palforge.Layout;
using Palforge.Memory;
using Palforge.Unreal.Names;
using Palforge.Unreal.Probes;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Stage;

internal static class LayoutDeriver
{
    private const int ReboundIntervalMilliseconds = 250;

    private static readonly string[] s_rootChain = ["Object", "Field", "Struct", "Class"];

    public static UnrealLayout Derive(IMemory memory, nint objectArray, INameResolver names, Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(names);

        progress?.Invoke("object array");
        var gate = ObjectArrayProbe.Probe(memory, objectArray);

        if (!gate.IsComplete || !ObjectArrayView.TryCreate(memory, objectArray, gate, out var view))
            return UnrealLayout.Failed(DerivationStage.ObjectArray, Describe(gate));

        var derived = new List<LayoutTable> { gate };

        progress?.Invoke("object base");
        var objectBase = new ObjectBaseProbe(memory, view, names).Probe();

        if (!HasAll(objectBase, LayoutNames.ClassPrivate, LayoutNames.NamePrivate, LayoutNames.OuterPrivate))
            return UnrealLayout.Failed(DerivationStage.ObjectBase, Describe(objectBase));

        derived.Add(objectBase);
        BoundNameIndices(memory, view, names, objectBase.OffsetOrThrow(LayoutNames.NamePrivate));

        progress?.Invoke("structs");
        var structs = new StructProbe(memory, view, names, Compose(derived).Build()).Probe();
        derived.Add(structs);

        progress?.Invoke("properties");
        var known = Compose(derived).Build();
        var properties = new PropertyProbe(memory, view, names, known).Probe();
        derived.Add(properties);

        progress?.Invoke("functions");
        known = Compose(derived).Build();
        var functions = new FunctionProbe(memory, view, names, known).Probe();
        derived.Add(functions);

        progress?.Invoke("composing");

        var builder = Compose(derived);
        var layout = builder.Build();

        if (!TryValidate(memory, view, names, layout, out var reason))
            return UnrealLayout.Failed(DerivationStage.Validation, reason);

        if (!ObjectArrayView.TryCreate(memory, objectArray, layout, out var complete))
            return UnrealLayout.Failed(DerivationStage.Validation, "the composed layout could not open the object array");

        AllowNameIndicesToGrow(memory, complete, names, layout.OffsetOrThrow(LayoutNames.NamePrivate));

        return UnrealLayout.Ready(layout, complete, names, Fingerprint(layout), builder.Conflicts);
    }

    private static LayoutBuilder Compose(IEnumerable<LayoutTable> derived)
    {
        var builder = new LayoutBuilder();

        foreach (var table in derived)
            builder.AddDerived(table);

        return builder.AddTable(UnrealVersionTable.Version, UnrealVersionTable.Offsets);
    }

    private static void BoundNameIndices(IMemory memory, ObjectArrayView view, INameResolver names, int namePrivate)
    {
        if (names is not NameResolver resolver)
            return;

        resolver.MaxComparisonIndex = HighestNameIndex(memory, view, namePrivate);
    }

    private static void AllowNameIndicesToGrow(IMemory memory, ObjectArrayView view, INameResolver names, int namePrivate)
    {
        if (names is not NameResolver resolver)
            return;

        var scanned = view.Count;
        var runningMax = resolver.MaxComparisonIndex;
        var lastAt = 0L;

        resolver.Rebounding = () =>
        {
            var now = Environment.TickCount64;

            if (now - lastAt < ReboundIntervalMilliseconds)
                return runningMax;

            lastAt = now;

            var count = view.Count;

            if (count <= scanned)
                return runningMax;

            foreach (var address in view.AddressesFrom(scanned))
            {
                if (memory.TryRead(address + namePrivate, out int id) && id > runningMax)
                    runningMax = id;
            }

            scanned = count;

            return runningMax;
        };
    }

    private static int HighestNameIndex(IMemory memory, ObjectArrayView view, int namePrivate)
    {
        var max = 0;

        foreach (var address in view.Addresses())
        {
            if (memory.TryRead(address + namePrivate, out int id) && id > max)
                max = id;
        }

        return max;
    }

    private static bool TryValidate(IMemory memory, ObjectArrayView view, INameResolver names, LayoutTable layout, out string reason)
    {
        reason = string.Empty;

        var namePrivate = layout.OffsetOrThrow(LayoutNames.NamePrivate);
        var superStruct = layout.OffsetOrThrow(LayoutNames.SuperStruct);
        var classDefaultObject = layout.OffsetOrThrow(LayoutNames.ClassDefaultObject);

        var located = new Dictionary<string, nint>(StringComparer.Ordinal);

        foreach (var address in view.Addresses())
        {
            var name = NameOf(memory, names, address, namePrivate);

            if (Array.IndexOf(s_rootChain, name) >= 0 && !located.ContainsKey(name))
                located[name] = address;
        }

        foreach (var name in s_rootChain)
        {
            if (!located.ContainsKey(name))
            {
                reason = $"the root class '{name}' was not found by walking the object array with the composed layout";
                return false;
            }
        }

        if (!memory.TryRead(located["Object"] + superStruct, out nint objectSuper) || objectSuper is not 0)
        {
            reason = "the 'Object' class does not have a null super struct";
            return false;
        }

        for (var index = 1; index < s_rootChain.Length; index++)
        {
            if (!memory.TryRead(located[s_rootChain[index]] + superStruct, out nint super) || super != located[s_rootChain[index - 1]])
            {
                reason = $"'{s_rootChain[index]}' does not derive from '{s_rootChain[index - 1]}'";
                return false;
            }
        }

        if (!memory.TryRead(located["Object"] + classDefaultObject, out nint cdo) || cdo is 0)
        {
            reason = $"'Object' has no default object at 0x{classDefaultObject:X} ({layout[LayoutNames.ClassDefaultObject].Provenance}). {FindDefaultObjectOffsets(memory, names, located, namePrivate)}";
            return false;
        }

        var cdoName = NameOf(memory, names, cdo, namePrivate);

        if (cdoName != "Default__Object")
        {
            reason = $"the default object of 'Object' is named '{cdoName}', not 'Default__Object' (0x{classDefaultObject:X}, {layout[LayoutNames.ClassDefaultObject].Provenance})";
            return false;
        }

        if (!ChildPropertiesHold(memory, layout, located["Class"]))
        {
            reason = "the ChildProperties offset does not lead to a field owned by the 'Class' struct";
            return false;
        }

        if (!ChildrenHold(memory, layout, located["Class"]))
        {
            reason = "the Children offset does not lead to a field owned by the 'Class' struct";
            return false;
        }

        return true;
    }

    private static string FindDefaultObjectOffsets(IMemory memory, INameResolver names, Dictionary<string, nint> located, int namePrivate)
    {
        var report = new List<string>();

        foreach (var name in (string[])["Object", "Class"])
        {
            var klass = located[name];
            var hits = new List<string>();

            for (var offset = 0; offset <= 0x200; offset += nint.Size)
            {
                if (!memory.TryRead(klass + offset, out nint value) || value is 0)
                    continue;

                var target = NameOf(memory, names, value, namePrivate);

                if (target.StartsWith("Default__", StringComparison.Ordinal))
                    hits.Add($"+{offset:X}={target}");
            }

            report.Add($"{name}[{string.Join(' ', hits)}]");
        }

        return string.Join(' ', report);
    }

    private static bool ChildPropertiesHold(IMemory memory, LayoutTable layout, nint klass)
    {
        var childProperties = layout.OffsetOrThrow(LayoutNames.ChildProperties);

        if (!memory.TryRead(klass + childProperties, out nint field) || field is 0)
            return true;

        for (var offset = 0; offset <= 0x30; offset += nint.Size)
        {
            if (memory.TryRead(field + offset, out nint owner) && owner == klass)
                return true;
        }

        return false;
    }

    private static bool ChildrenHold(IMemory memory, LayoutTable layout, nint klass)
    {
        var children = layout.OffsetOrThrow(LayoutNames.Children);
        var outerPrivate = layout.OffsetOrThrow(LayoutNames.OuterPrivate);

        if (!memory.TryRead(klass + children, out nint field) || field is 0)
            return true;

        return memory.TryRead(field + outerPrivate, out nint owner) && owner == klass;
    }

    private static string NameOf(IMemory memory, INameResolver names, nint address, int namePrivate)
    {
        return memory.TryRead(address + namePrivate, out int id) && names.TryResolve(id, out var name) ? name : string.Empty;
    }

    private static bool HasAll(LayoutTable layout, params string[] members)
    {
        return Array.TrueForAll(members, member => layout.TryGetOffset(member, out _));
    }

    private static string Describe(LayoutTable layout)
    {
        return string.Join("; ", layout.Unusable().Select(static member => member.ToString()));
    }

    private static string Fingerprint(LayoutTable layout)
    {
        var builder = new StringBuilder(UnrealVersionTable.Version);

        foreach (var member in layout.Members.Where(static member => member.IsVerified).OrderBy(static member => member.Name, StringComparer.Ordinal))
        {
            if (member.TryGetOffset(out var offset))
                builder.Append('\n').Append(member.Name).Append('=').Append(offset.ToString("X", CultureInfo.InvariantCulture));
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())).AsSpan(0, 8));
    }
}