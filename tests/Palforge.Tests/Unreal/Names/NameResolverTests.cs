using Palforge.Tests.Memory;
using Palforge.Tests.Unreal.Probes;
using Palforge.Unreal.Names;

namespace Palforge.Tests.Unreal.Names;

public sealed class NameResolverTests
{
    private const int Scratch = 4096;

    [Fact]
    public void AnInternedNameRoundTrips()
    {
        var (resolver, names, _) = Build();
        var expected = names.Intern("Class");

        Assert.True(resolver.TryFind("Class", out var id));
        Assert.Equal(expected, id);

        Assert.True(resolver.TryResolve(id, out var name));
        Assert.Equal("Class", name);
    }

    [Fact]
    public void ADefaultObjectNameSurvivesTheWideRoundTrip()
    {
        var (resolver, names, _) = Build();
        names.Intern("Default__Actor");

        Assert.True(resolver.TryFind("Default__Actor", out var id));
        Assert.True(resolver.TryResolve(id, out var name));
        Assert.Equal("Default__Actor", name);
    }

    [Fact]
    public void AMissingNameIsNotFoundRatherThanInvented()
    {
        var (resolver, _, natives) = Build();

        Assert.False(resolver.TryFind("NeverInterned", out var id));
        Assert.Equal(0, id);
        Assert.Equal(1, natives.ConstructCalls);
    }

    [Fact]
    public void NoneResolvesEvenThoughItsIndexIsZero()
    {
        var (resolver, _, _) = Build();

        Assert.True(resolver.TryFind("None", out var id));
        Assert.Equal(0, id);

        Assert.True(resolver.TryResolve(0, out var name));
        Assert.Equal("None", name);
    }

    [Fact]
    public void LookupsAreCachedAndDoNotCallTheNativesTwice()
    {
        var (resolver, names, natives) = Build();
        names.Intern("Struct");

        Assert.True(resolver.TryFind("Struct", out var first));
        Assert.True(resolver.TryFind("Struct", out var second));

        Assert.Equal(first, second);
        Assert.Equal(1, natives.ConstructCalls);
    }

    [Fact]
    public void ResolvingIsCachedAcrossBothDirections()
    {
        var (resolver, names, natives) = Build();
        var id = names.Intern("Actor");

        Assert.True(resolver.TryResolve(id, out _));
        Assert.True(resolver.TryFind("Actor", out _));

        Assert.Equal(0, natives.ConstructCalls);
        Assert.Equal(1, natives.ToStringCalls);
    }

    [Fact]
    public void AScratchThatIsTooSmallIsRejected()
    {
        var memory = new FakeMemory();
        var names = new FakeNameTable();
        var natives = new FakeFNameNatives(memory, names);

        Assert.Throws<ArgumentOutOfRangeException>(() => new NameResolver(memory, natives, memory.Allocate(64), 64));
    }

    [Fact]
    public void EveryAnchorNameTheProbesNeedRoundTrips()
    {
        var (resolver, names, _) = Build();

        foreach (var name in (string[])["Class", "Struct", "Field", "Object", "ScriptStruct", "Function", "Enum", "Default__Class"])
        {
            names.Intern(name);

            Assert.True(resolver.TryFind(name, out var id), name);
            Assert.True(resolver.TryResolve(id, out var resolved), name);
            Assert.Equal(name, resolved);
        }
    }

    [Fact]
    public void InterningANewNameAddsItToThePoolAndYieldsItsFName()
    {
        var (resolver, names, _) = Build();

        Assert.True(resolver.TryIntern("BrandNewName", out var fname));
        Assert.True(names.TryFind("BrandNewName", out var id));
        Assert.Equal(id, (int)fname);
    }

    [Fact]
    public void InterningPacksTheComparisonIndexInTheLowHalfOfTheFName()
    {
        var (resolver, names, _) = Build();
        var expected = names.Intern("Packed");

        Assert.True(resolver.TryIntern("Packed", out var fname));
        Assert.Equal(expected, (int)fname);
        Assert.Equal(0, (int)(fname >> 32));
    }

    [Fact]
    public void InterningLiftsTheProbeCapSoTheNewNameResolves()
    {
        var (resolver, _, _) = Build();
        resolver.MaxComparisonIndex = 0;

        Assert.True(resolver.TryIntern("PastTheCap", out var fname));
        Assert.True(resolver.MaxComparisonIndex >= (int)fname);
        Assert.True(resolver.TryResolve((int)fname, out var name));
        Assert.Equal("PastTheCap", name);
    }

    private static (NameResolver Resolver, FakeNameTable Names, FakeFNameNatives Natives) Build()
    {
        var memory = new FakeMemory();
        var names = new FakeNameTable();
        var natives = new FakeFNameNatives(memory, names);
        var scratch = memory.Allocate(Scratch);

        return (new NameResolver(memory, natives, scratch, Scratch), names, natives);
    }
}