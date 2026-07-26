namespace Palforge.Layout;

internal sealed class LayoutBuilder
{
    private readonly Dictionary<string, LayoutMember> _members;
    private readonly List<string> _conflicts;

    public IReadOnlyList<string> Conflicts =>
        _conflicts;

    public LayoutBuilder()
    {
        _members = new Dictionary<string, LayoutMember>(StringComparer.Ordinal);
        _conflicts = [];
    }

    public LayoutBuilder Add(LayoutMember member)
    {
        var known = _members.TryGetValue(member.Name, out var current) && current.IsKnown;

        if (!known || member.IsVerified)
            _members[member.Name] = member;

        return this;
    }

    public LayoutBuilder AddDerived(LayoutTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        foreach (var member in table.Members)
            Add(member);

        return this;
    }

    public LayoutBuilder AddTable(string version, IReadOnlyDictionary<string, int> offsets)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(offsets);

        foreach (var (name, offset) in offsets)
        {
            if (_members.TryGetValue(name, out var member) && member.IsVerified)
            {
                if (member.TryGetOffset(out var derived) && derived != offset)
                    _conflicts.Add($"{name}: derived 0x{derived:X} but the {version} table says 0x{offset:X}");

                continue;
            }

            _members[name] = LayoutMember.Tabled(name, offset, version);
        }

        return this;
    }

    public LayoutTable Build()
    {
        return new LayoutTable(_members.Values);
    }
}