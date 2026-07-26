namespace Palforge.Layout;

internal sealed class LayoutTable
{
    private readonly Dictionary<string, LayoutMember> _members;

    public IReadOnlyCollection<LayoutMember> Members =>
        _members.Values;

    public int Declared =>
        _members.Count;

    public int Known =>
        _members.Values.Count(static member => member.IsKnown);

    public int Verified =>
        _members.Values.Count(static member => member.IsVerified);

    public int Undetermined =>
        _members.Values.Count(static member => member.Provenance is Provenance.Undetermined);

    public int NotAttempted =>
        _members.Values.Count(static member => member.Provenance is Provenance.NotAttempted);

    public bool IsComplete =>
        Declared > 0 && Known == Declared;

    public bool IsFullyVerified =>
        Declared > 0 && Verified == Declared;

    public LayoutMember this[string name] =>
        _members.TryGetValue(name, out var member)
            ? member
            : throw new KeyNotFoundException($"'{name}' is not a declared LayoutTable member.");

    public LayoutTable(IEnumerable<LayoutMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        _members = members.ToDictionary(static member => member.Name, StringComparer.Ordinal);

        if (_members.Count is 0)
            throw new ArgumentException("A LayoutTable must declare at least one member.", nameof(members));
    }

    public bool TryGetOffset(string name, out int offset)
    {
        offset = -1;

        return _members.TryGetValue(name, out var member) && member.TryGetOffset(out offset);
    }

    public int OffsetOrThrow(string name)
    {
        return this[name].OffsetOrThrow();
    }

    public IEnumerable<LayoutMember> Unusable()
    {
        return _members.Values.Where(static member => !member.IsKnown);
    }

    public IEnumerable<LayoutMember> Unverified()
    {
        return _members.Values.Where(static member => member is { IsKnown: true, IsVerified: false });
    }
}