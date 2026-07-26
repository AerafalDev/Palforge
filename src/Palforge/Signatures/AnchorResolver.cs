using Palforge.Images;
using Palforge.Memory;

namespace Palforge.Signatures;

internal sealed class AnchorResolver
{
    private readonly IMemory _memory;
    private readonly ModuleImage _image;

    public AnchorResolver(IMemory memory, ModuleImage image)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(image);

        _memory = memory;
        _image = image;
    }

    public Resolution<nint> Resolve(Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var candidates = new List<Candidate<nint>>();

        foreach (var section in _image.ExecutableSections)
            Collect(anchor, section, candidates);

        var resolution = Unanimity.EnsureOne(candidates, anchor.Patterns.Count);

        if (!resolution.TryGetValue(out var resolved))
            return resolution;

        return anchor.Verify(new AnchorContext(_memory, _image, resolved))
            ? resolution
            : Resolution<nint>.Unverified(resolved, resolution.Agreeing, resolution.Attempted, resolution.Sites, $"{anchor.Name} was rejected by its verification predicate");
    }

    public ScanReport Scan(Anchor anchor, ImageSection section)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        return PatternScanner.Scan(_memory, section.Address, section.Size, [.. anchor.Patterns.Select(static pattern => pattern.Pattern)]);
    }

    private void Collect(Anchor anchor, ImageSection section, List<Candidate<nint>> candidates)
    {
        var report = Scan(anchor, section);

        for (var index = 0; index < anchor.Patterns.Count; index++)
        {
            var kind = anchor.Patterns[index].Kind;

            foreach (var match in report.MatchesOf(index))
            {
                if (kind is AnchorKind.Direct)
                {
                    candidates.Add(new Candidate<nint>(index, match));
                    continue;
                }

                if (Rip.TryResolve(_memory, match, out var target))
                    candidates.Add(new Candidate<nint>(index, target));
            }
        }
    }
}