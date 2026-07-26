namespace Palforge.Signatures;

internal sealed class Anchor
{
    public string Name { get; }

    public Func<AnchorContext, bool> Verify { get; }

    public IReadOnlyList<AnchorPattern> Patterns { get; }

    public Anchor(string name, Func<AnchorContext, bool> verify, params AnchorPattern[] patterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(verify);
        ArgumentNullException.ThrowIfNull(patterns);

        if (patterns.Length is 0)
            throw new ArgumentException($"Anchor '{name}' declares no patterns.", nameof(patterns));

        Name = name;
        Verify = verify;
        Patterns = patterns;
    }
}