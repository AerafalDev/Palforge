namespace Palforge.Tests.Unreal.Probes;

internal sealed record FakeStructSpec(string Name, int PropertiesSize, IReadOnlyList<FakePropertySpec> Properties);