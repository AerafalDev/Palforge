namespace Palforge.Unreal.Probes;

internal sealed record PropertyAnchor(string Struct, string[] Members, int[] Offsets, int ElementSize);