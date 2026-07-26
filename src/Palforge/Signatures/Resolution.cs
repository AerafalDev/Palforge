namespace Palforge.Signatures;

internal readonly record struct Resolution<T>
{
    private readonly T? _value;

    public ResolutionStatus Status { get; }

    public int Agreeing { get; }

    public int Attempted { get; }

    public int Sites { get; }

    public string? Detail { get; }

    public bool IsResolved =>
        Status is ResolutionStatus.Resolved;

    private Resolution(ResolutionStatus status, T? value, int agreeing, int attempted, int sites, string? detail)
    {
        _value = value;

        Status = status;
        Agreeing = agreeing;
        Attempted = attempted;
        Sites = sites;
        Detail = detail;
    }

    public static Resolution<T> Resolved(T value, int agreeing, int attempted, int sites)
    {
        return new Resolution<T>(ResolutionStatus.Resolved, value, agreeing, attempted, sites, null);
    }

    public static Resolution<T> NotFound(int attempted)
    {
        return new Resolution<T>(ResolutionStatus.NotFound, default, 0, attempted, 0, null);
    }

    public static Resolution<T> Ambiguous(int distinct, int attempted, string detail)
    {
        return new Resolution<T>(ResolutionStatus.Ambiguous, default, distinct, attempted, 0, detail);
    }

    public static Resolution<T> Unverified(T value, int agreeing, int attempted, int sites, string detail)
    {
        return new Resolution<T>(ResolutionStatus.Unverified, value, agreeing, attempted, sites, detail);
    }

    public bool TryGetValue(out T value)
    {
        value = IsResolved ? _value! : default!;

        return IsResolved;
    }

    public T ValueOrThrow()
    {
        return IsResolved
            ? _value!
            : throw new InvalidOperationException($"Resolution failed: {Status}. {Detail}".TrimEnd());
    }

    public override string ToString()
    {
        return Status switch
        {
            ResolutionStatus.Resolved => $"resolved to {Describe(_value)} ({Agreeing}/{Attempted} candidates, {Sites} sites)",
            ResolutionStatus.NotFound => $"not found (0/{Attempted} candidates matched)",
            ResolutionStatus.Ambiguous => $"ambiguous across {Attempted} candidates: {Detail}",
            _ => $"found {Describe(_value)} but failed verification: {Detail}"
        };
    }

    internal static string Describe(T? value)
    {
        return value is nint address
            ? $"0x{address:X}"
            : value?.ToString() ?? "<none>";
    }
}