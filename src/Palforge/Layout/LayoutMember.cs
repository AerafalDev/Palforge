namespace Palforge.Layout;

internal readonly record struct LayoutMember
{
    private readonly int _offset;

    public string Name { get; }

    public Provenance Provenance { get; }

    public int Candidates { get; }

    public string? Detail { get; }

    public bool IsKnown =>
        Provenance is not (Provenance.Undetermined or Provenance.NotAttempted);

    public bool IsVerified =>
        Provenance is Provenance.Derived;

    private LayoutMember(string name, int offset, Provenance provenance, int candidates, string? detail)
    {
        _offset = offset;

        Name = name;
        Provenance = provenance;
        Candidates = candidates;
        Detail = detail;
    }

    public static LayoutMember Derived(string name, int offset)
    {
        return new LayoutMember(name, offset, Provenance.Derived, 0, null);
    }

    public static LayoutMember Tabled(string name, int offset, string version)
    {
        return new LayoutMember(name, offset, Provenance.Tabled, 0, version);
    }

    public static LayoutMember NotAttempted(string name, string reason)
    {
        return new LayoutMember(name, -1, Provenance.NotAttempted, 0, reason);
    }

    public static LayoutMember Undetermined(string name, int candidates, string reason)
    {
        return new LayoutMember(name, -1, Provenance.Undetermined, candidates, reason);
    }

    public bool TryGetOffset(out int offset)
    {
        offset = IsKnown ? _offset : -1;

        return IsKnown;
    }

    public int OffsetOrThrow()
    {
        return IsKnown
            ? _offset
            : throw new InvalidOperationException($"'{Name}' has no offset: {Provenance}. {Detail}".TrimEnd());
    }

    public override string ToString()
    {
        return Provenance switch
        {
            Provenance.Derived => $"{Name} = 0x{_offset:X} (derived)",
            Provenance.Tabled => $"{Name} = 0x{_offset:X} (from the {Detail} table, NOT verified against this build)",
            Provenance.NotAttempted => $"{Name} = <not attempted: {Detail}>",
            _ => $"{Name} = <undetermined after {Candidates} candidates: {Detail}>"
        };
    }
}