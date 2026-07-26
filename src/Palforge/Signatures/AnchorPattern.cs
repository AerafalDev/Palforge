namespace Palforge.Signatures;

internal sealed class AnchorPattern
{
    public Pattern Pattern { get; }

    public AnchorKind Kind { get; }

    private AnchorPattern(Pattern pattern, AnchorKind kind)
    {
        Pattern = pattern;
        Kind = kind;
    }

    public static AnchorPattern Direct(string text)
    {
        return new AnchorPattern(Pattern.Parse(text), AnchorKind.Direct);
    }

    public static AnchorPattern Rip(string text)
    {
        return new AnchorPattern(Pattern.Parse(text), AnchorKind.RipRelative);
    }
}