namespace Palforge.Unreal.Sdk;

internal sealed record SdkClass(string Namespace, string Name, string BaseName, IReadOnlyList<SdkProperty> Properties, IReadOnlyList<SdkMethod> Methods, string? UeName = null, bool IsActor = false, bool IsStruct = false);
