namespace Palforge.Tests.Unreal.Probes;

internal sealed record FakePropertySpec(string Name, string FieldClass, int Offset, int ElementSize, string? Target = null);