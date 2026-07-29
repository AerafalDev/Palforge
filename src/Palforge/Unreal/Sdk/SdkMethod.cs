namespace Palforge.Unreal.Sdk;

internal sealed record SdkMethod(
    string Name,
    string UeName,
    string OwnerUeName,
    bool IsStatic,
    IReadOnlyList<SdkParameter> Parameters,
    string ReturnType,
    string? ReturnReader,
    string? ReturnStruct = null);