using Palforge.Images;
using Palforge.Signatures;
using Palforge.Signatures.Anchors;
using Palforge.Signatures.Resolvers;
using Palforge.Tests.Images;
using Palforge.Tests.Memory;
using Palforge.Unreal.Stage;

namespace Palforge.Tests.Signatures;

public sealed class PalforgeBinaryAnchorTests
{
    private const string DefaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\PalServer\Pal\Binaries\Win64\PalServer-Win64-Shipping.exe";

    private static readonly nint s_imageBase = unchecked((nint)0x1_4000_0000L);

    private readonly ITestOutputHelper _output;

    public PalforgeBinaryAnchorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TheAnchorSetResolvesEverythingDerivationNeeds()
    {
        var module = Load();
        var anchors = AnchorSet.Resolve(Memory(), module);

        _output.WriteLine($"engine        {anchors.EngineVersion}");
        _output.WriteLine($"GUObjectArray {anchors.GUObjectArray}");
        _output.WriteLine($"FName::FName  {anchors.FNameConstructor}");
        _output.WriteLine($"ToString      {anchors.FNameToString}");
        _output.WriteLine($"GMalloc       {anchors.GMalloc}");
        _output.WriteLine($"Tick          {anchors.GameEngineTick}");

        Assert.True(anchors.DerivationReady);
    }

    [Fact]
    public void TheServerBinaryParsesAsAPeImage()
    {
        var module = Load();

        Assert.True(module.Size > 100 * 1024 * 1024);
        Assert.Contains(module.Sections, static section => section.Name is ".text");
    }

    [Fact]
    public void GUObjectArrayResolves()
    {
        Resolve(UnrealAnchors.GUObjectArray);
    }

    [Fact]
    public void FNameToStringOutResolves()
    {
        Resolve(UnrealAnchors.FNameToStringOut);
    }

    [Fact]
    public void FNameToStringReturnResolves()
    {
        Resolve(UnrealAnchors.FNameToStringReturn);
    }

    [Fact]
    public void TheAnchorLiteralsAreFoundInTheImage()
    {
        var resolver = new FNameConstructorResolver(Memory(), Load());
        var literals = resolver.FindLiterals();

        foreach (var address in literals)
            _output.WriteLine($"literal at 0x{address:X}");

        Assert.NotEmpty(literals);
    }

    [Fact]
    public void FNameConstructorResolvesThroughStringXrefs()
    {
        var resolution = new FNameConstructorResolver(Memory(), Load()).Resolve();

        _output.WriteLine(resolution.ToString());

        Assert.Equal(ResolutionStatus.Resolved, resolution.Status);
    }

    [Fact]
    public void GMallocResolves()
    {
        Resolve(UnrealAnchors.GMalloc);
    }

    [Fact]
    public void TheEngineVersionIsUnrealEngine5()
    {
        var module = Load();
        var resolution = new EngineVersionResolver(Memory(), module).Resolve();

        _output.WriteLine(resolution.ToString());

        Assert.True(resolution.TryGetValue(out var version));
        Assert.Equal(5, version.Major);
    }

    [Fact]
    public void GameEngineTickResolvesThroughStringXrefsAndUnwindData()
    {
        var resolution = new GameEngineTickResolver(Memory(), Load()).Resolve();

        _output.WriteLine(resolution.ToString());

        Assert.Equal(ResolutionStatus.Resolved, resolution.Status);
    }

    [Fact]
    public void TheTwoToStringOverloadsAreDistinctFunctions()
    {
        var module = Load();
        var resolver = new AnchorResolver(Memory(), module);

        Assert.True(resolver.Resolve(UnrealAnchors.FNameToStringOut).TryGetValue(out var withOut));
        Assert.True(resolver.Resolve(UnrealAnchors.FNameToStringReturn).TryGetValue(out var returning));

        Assert.NotEqual(withOut, returning);
    }

    private void Resolve(Anchor anchor)
    {
        var module = Load();
        var resolver = new AnchorResolver(Memory(), module);

        foreach (var section in module.ExecutableSections)
        {
            var report = resolver.Scan(anchor, section);

            _output.WriteLine($"{anchor.Name} over {section.Name} ({section.Size / (1024 * 1024)} MiB) in {report.Elapsed.TotalMilliseconds:N1} ms");

            for (var index = 0; index < anchor.Patterns.Count; index++)
                _output.WriteLine($"  [{anchor.Patterns[index].Kind,-11}] {report.MatchesOf(index).Count,4} matches  {anchor.Patterns[index].Pattern.Text}");
        }

        var resolution = resolver.Resolve(anchor);

        _output.WriteLine($"  -> {resolution}");

        Assert.Equal(ResolutionStatus.Resolved, resolution.Status);
    }

    private static ModuleImage Load()
    {
        Assert.True(ModuleImage.TryParse(Memory(), s_imageBase, out var module));

        return module;
    }

    private static BufferMemory Memory()
    {
        var path = Path();

        Assert.SkipWhen(!File.Exists(path), $"the Palworld server binary is not installed at '{path}'");

        return new BufferMemory(PeFileMapper.Map(path), s_imageBase);
    }

    private static string Path()
    {
        return Environment.GetEnvironmentVariable("PALWORLD_SERVER_EXE") ?? DefaultPath;
    }
}