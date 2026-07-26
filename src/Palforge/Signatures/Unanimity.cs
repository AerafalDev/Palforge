namespace Palforge.Signatures;

internal static class Unanimity
{
    private const int MaxReported = 4;

    public static Resolution<T> EnsureOne<T>(IReadOnlyCollection<Candidate<T>> candidates, int attempted)
    {
        if (candidates.Count is 0)
            return Resolution<T>.NotFound(attempted);

        var distinct = new List<T>(MaxReported + 1);

        foreach (var candidate in candidates)
        {
            if (distinct.Contains(candidate.Value))
                continue;

            distinct.Add(candidate.Value);

            if (distinct.Count > MaxReported)
                break;
        }

        if (distinct.Count is not 1)
            return Resolution<T>.Ambiguous(distinct.Count, attempted, Describe(distinct));

        var patterns = candidates.Select(static candidate => candidate.PatternIndex).Distinct().Count();

        return Resolution<T>.Resolved(distinct[0], patterns, attempted, candidates.Count);
    }

    private static string Describe<T>(List<T> distinct)
    {
        var reported = distinct.Take(MaxReported).Select(static value => Resolution<T>.Describe(value));

        return distinct.Count > MaxReported
            ? $">={MaxReported} distinct values [{string.Join(", ", reported)}, ...]"
            : $"{distinct.Count} distinct values [{string.Join(", ", reported)}]";
    }
}