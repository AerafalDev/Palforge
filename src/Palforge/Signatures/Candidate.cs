namespace Palforge.Signatures;

internal readonly record struct Candidate<T>(int PatternIndex, T Value);