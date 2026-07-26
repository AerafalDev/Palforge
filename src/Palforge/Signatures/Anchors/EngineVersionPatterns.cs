namespace Palforge.Signatures.Anchors;

internal static class EngineVersionPatterns
{
    public static readonly IReadOnlyList<Pattern> All =
    [
        Pattern.Parse("C7 47 20 | 04 00 ?? 00 66 89 6F 24"),
        Pattern.Parse("C7 4? 20 | 04 00 ?? ?? 66 4? 89 ?? 24"),
        Pattern.Parse("C7 ?? 24 20 | 04 00 ?? ?? 48 8D 45 F0"),
        Pattern.Parse("C7 05 ?? ?? ?? ?? | 04 00 ?? 00 66 89 ?? ?? ?? ?? ?? C7 05"),
        Pattern.Parse("C7 05 ?? ?? ?? ?? | 04 00 ?? 00 66 89 ?? ?? ?? ?? ?? 89"),
        Pattern.Parse("41 C7 ?? | 04 00 ?? 00 ?? ?? 00 00 00 66 41 89"),
        Pattern.Parse("41 C7 ?? | 04 00 18 00 66 41 89 ?? 04"),
        Pattern.Parse("41 C7 04 24 | 04 00 ?? 00 66 ?? 89 ?? 24"),
        Pattern.Parse("41 C7 04 24 | 04 00 ?? 00 B9 ?? 00 00 00"),
        Pattern.Parse("41 C7 44 24 20 | 04 00 ?? 00 66 ?? 89 ?? 24"),
        Pattern.Parse("C7 05 ?? ?? ?? ?? | 04 00 ?? 00 89 3D ?? ?? ?? ?? 85 FF"),
        Pattern.Parse("C7 05 ?? ?? ?? ?? | 04 00 ?? 00 89 05 ?? ?? ?? ?? E8"),
        Pattern.Parse("C7 05 ?? ?? ?? ?? | 04 00 ?? 00 66 89 ?? ?? ?? ?? ??"),
        Pattern.Parse("C7 46 20 | 04 00 ?? 00 66 44 89 76 24 44 89 76 28 48 39 C7"),
        Pattern.Parse("C7 03 | 04 00 ?? 00 66 44 89 63 04 C7 43 08 C1 5C 08 80 E8"),
        Pattern.Parse("C7 47 20 | 04 00 ?? 00 66 89 6F 24 C7 47 28 ?? ?? ?? ?? 49"),
        Pattern.Parse("C7 03 | 04 00 ?? 00 66 89 6B 04 89 7B 08 48 83 C3 10"),
        Pattern.Parse("41 C7 06 | 05 00 ?? ?? 48 8B 5C 24 ?? 49 8D 76 ?? 33 ED 41 89 46"),
        Pattern.Parse("C7 06 | 05 00 ?? ?? 48 8B 5C 24 20 4C 8D 76 10 33 ED"),
        Pattern.Parse("11 76 30 C7 46 20 | 04 00 ?? 00"),
        Pattern.Parse("0F 57 C0 0F 11 43 10 C7 03 | 05 ?? ?? ?? 66 C7 43 04 ?? ??"),
        Pattern.Parse("48 89 2? 48 89 6? 08 C7 0? | 05 00 ?? ?? 66"),
        Pattern.Parse("49 89 2? 49 89 6? 08 C7 0? | 05 00 ?? ?? 66"),
        Pattern.Parse("C7 46 20 | 05 00 ?? ?? 66 89 ?? 24"),
        Pattern.Parse("C7 43 20 | 05 00 ?? ?? 48 3B F0"),
        Pattern.Parse("C7 46 20 | 05 00 ?? ?? 48 8D 44 24 20"),
        Pattern.Parse("C7 4? 20 | 05 00 ?? ?? 66 44 89 ?? 24"),
        Pattern.Parse("C7 ?? 24 20 | 05 00 ?? ?? 48 8D 45 F0"),
        Pattern.Parse("C7 06 | 05 00 ?? 00 66 C7 46 04"),
        Pattern.Parse("0F B6 D8 C1 E3 1F E8 ?? ?? ?? ?? 0B C3 C7 06 | 05 00 ?? 00"),
        Pattern.Parse("0F B6 D8 C1 E3 1F E8 ?? ?? ?? ?? 0B C3 C7 06 | 04 00 ?? 00"),
        Pattern.Parse("0F B6 D8 C1 E3 1F E8 ?? ?? ?? ?? 33 ED C7 06 | 05 00 ?? 00"),
        Pattern.Parse("89 2E 89 6E 08 48 8D 4E 0C 89 29 41 C7 07 | 05 00 ?? 00")
    ];
}