using System.Globalization;
using Palforge.Images;
using Palforge.Memory;

namespace Palforge.Signatures.Resolvers;

internal abstract class LiteralXrefResolver
{
    protected IMemory Memory { get; }

    protected ModuleImage Image { get; }

    protected abstract IReadOnlyList<string> Literals { get; }

    protected abstract IReadOnlyList<string> Templates { get; }

    protected abstract string UnverifiedReason { get; }

    protected LiteralXrefResolver(IMemory memory, ModuleImage image)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(image);

        Memory = memory;
        Image = image;
    }

    protected abstract bool TryProject(nint match, out nint target);

    protected virtual Resolution<nint>? Prelude()
    {
        return null;
    }

    public Resolution<nint> Resolve()
    {
        if (Prelude() is { IsResolved: true } prelude)
            return prelude;

        var strings = FindLiterals();

        if (strings.Count is 0)
            return Resolution<nint>.NotFound(Templates.Count);

        var patterns = Build(strings);
        var candidates = new List<Candidate<nint>>();

        foreach (var section in Image.ExecutableSections)
        {
            var report = PatternScanner.Scan(Memory, section.Address, section.Size, patterns);

            for (var index = 0; index < patterns.Count; index++)
            {
                foreach (var match in report.MatchesOf(index))
                {
                    if (TryProject(match, out var target))
                        candidates.Add(new Candidate<nint>(index % Templates.Count, target));
                }
            }
        }

        var resolution = Unanimity.EnsureOne(candidates, patterns.Count);

        if (!resolution.TryGetValue(out var resolved))
            return resolution;

        return new AnchorContext(Memory, Image, resolved).IsInExecutableSection()
            ? resolution
            : Resolution<nint>.Unverified(resolved, resolution.Agreeing, resolution.Attempted, resolution.Sites, UnverifiedReason);
    }

    public List<nint> FindLiterals()
    {
        var patterns = Literals.Select(Pattern.Utf16).ToArray();
        var found = new List<nint>();

        foreach (var section in Image.ReadableSections)
        {
            var report = PatternScanner.Scan(Memory, section.Address, section.Size, patterns);

            for (var index = 0; index < patterns.Length; index++)
                found.AddRange(report.MatchesOf(index));
        }

        return found;
    }

    private List<Pattern> Build(List<nint> strings)
    {
        var patterns = new List<Pattern>(strings.Count * Templates.Count);

        foreach (var address in strings)
        {
            var hex = address.ToString("X", CultureInfo.InvariantCulture);

            foreach (var template in Templates)
                patterns.Add(Pattern.Parse(string.Format(CultureInfo.InvariantCulture, template, hex)));
        }

        return patterns;
    }
}