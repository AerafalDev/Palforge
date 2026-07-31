using Palforge.Unreal.Reflection;
using Palforge.Unreal.Sdk;

namespace Palforge.Tests.Unreal.Sdk;

public sealed class SdkMethodMapArrayTests
{
    private const EPropertyFlags Out = EPropertyFlags.Parm | EPropertyFlags.OutParm | EPropertyFlags.ReferenceParm;
    private const EPropertyFlags In = EPropertyFlags.Parm;

    [Fact]
    public void AnOutArrayOfObjectsComesBackAsATypedArray()
    {
        var element = new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "Actor");
        var mapped = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16, Element: element), "outActors", Out);

        Assert.Equal("Actor[]", mapped?.TypeName);
        Assert.Equal("out", mapped?.Modifier);
        Assert.Equal("SdkEnv.Objects<Actor>(#)", mapped?.Output);
    }

    [Fact]
    public void AnOutArrayOfScalarsComesBackAsAValueArray()
    {
        var element = new SdkPropertyFacts(PropertyKind.Int32, 0x0, 4);
        var mapped = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16, Element: element), "values", Out);

        Assert.Equal("int[]", mapped?.TypeName);
        Assert.Equal("SdkEnv.Values<int>(#)", mapped?.Output);
    }

    [Fact]
    public void AnOutArrayOfAnUntypedObjectFallsBackToUObject()
    {
        var element = new SdkPropertyFacts(PropertyKind.Object, 0x0, 8);
        var mapped = SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16, Element: element), "objects", Out);

        Assert.Equal("UObject[]", mapped?.TypeName);
    }

    [Fact]
    public void AnOutArrayOfStringsIsStillSkipped()
    {
        var element = new SdkPropertyFacts(PropertyKind.Str, 0x0, 16);

        Assert.Null(SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16, Element: element), "names", Out));
    }

    [Fact]
    public void AnInputArrayIsStillSkipped()
    {
        var element = new SdkPropertyFacts(PropertyKind.Object, 0x0, 8, "Actor");

        Assert.Null(SdkMethodMap.Parameter(new SdkPropertyFacts(PropertyKind.Array, 0x0, 16, Element: element), "actors", In));
    }
}