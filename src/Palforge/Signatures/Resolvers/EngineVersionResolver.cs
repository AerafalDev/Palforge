using Palforge.Images;
using Palforge.Memory;
using Palforge.Signatures.Anchors;

namespace Palforge.Signatures.Resolvers;

internal sealed class EngineVersionResolver
{
    private readonly IMemory _memory;
    private readonly ModuleImage _image;

    public EngineVersionResolver(IMemory memory, ModuleImage image)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(image);

        _memory = memory;
        _image = image;
    }

    public Resolution<EngineVersion> Resolve()
    {
        var patterns = EngineVersionPatterns.All;
        var candidates = new List<Candidate<EngineVersion>>();

        foreach (var section in _image.ExecutableSections)
        {
            var report = PatternScanner.Scan(_memory, section.Address, section.Size, patterns);

            for (var index = 0; index < patterns.Count; index++)
            {
                foreach (var match in report.MatchesOf(index))
                {
                    if (!TryReadVersion(match, out var version) || !version.IsPlausible)
                        continue;

                    candidates.Add(new Candidate<EngineVersion>(index, version));
                }
            }
        }

        return Unanimity.EnsureOne(candidates, patterns.Count);
    }

    private bool TryReadVersion(nint address, out EngineVersion version)
    {
        version = default;

        if (!_memory.TryRead(address, out ushort major) || !_memory.TryRead(address + 2, out ushort minor))
            return false;

        version = new EngineVersion(major, minor);

        return true;
    }
}