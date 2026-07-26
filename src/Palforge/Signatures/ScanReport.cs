namespace Palforge.Signatures;

internal sealed class ScanReport
{
    private readonly List<nint>[] _matches;

    public IReadOnlyList<Pattern> Patterns { get; }

    public TimeSpan Elapsed { get; }

    public int Attempted =>
        Patterns.Count;

    public int MatchedPatterns =>
        _matches.Count(static matches => matches.Count > 0);

    public ScanReport(IReadOnlyList<Pattern> patterns, List<nint>[] matches, TimeSpan elapsed)
    {
        _matches = matches;

        Patterns = patterns;
        Elapsed = elapsed;
    }

    public IReadOnlyList<nint> MatchesOf(int patternIndex)
    {
        return _matches[patternIndex];
    }
}