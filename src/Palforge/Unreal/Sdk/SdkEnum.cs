namespace Palforge.Unreal.Sdk;

internal sealed record SdkEnum(string Namespace, string Name, IReadOnlyList<SdkEnumMember> Members, string Underlying = "int");