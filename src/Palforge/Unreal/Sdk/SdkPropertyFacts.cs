using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Sdk;

internal sealed record SdkPropertyFacts(
    PropertyKind Kind,
    int Offset,
    int ElementSize,
    string? ReferencedType = null,
    byte BoolMask = 0xFF,
    SdkPropertyFacts? Element = null,
    SdkPropertyFacts? Key = null,
    SdkPropertyFacts? Value = null,
    int Stride = 0,
    int ValueOffset = 0,
    string? Name = null);