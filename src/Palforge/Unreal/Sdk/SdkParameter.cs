namespace Palforge.Unreal.Sdk;

internal sealed record SdkParameter(string Name, string TypeName, string Marshal, string Modifier = "", string? Output = null, string? Destination = null);
