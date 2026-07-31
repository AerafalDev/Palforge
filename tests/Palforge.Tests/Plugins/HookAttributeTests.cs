using Palforge.Hooks.Attributes;
using Palforge.Unreal.Reflection;

namespace Palforge.Tests.Plugins;

public sealed class HookAttributeTests
{
    [Fact]
    public void AOneStringTargetSplitsIntoClassAndFunction()
    {
        var attribute = new PreHookAttribute("PalPlayerCharacter:TakeDamage");

        Assert.Equal("PalPlayerCharacter", attribute.ClassName);
        Assert.Equal("TakeDamage", attribute.FunctionName);
        Assert.Null(attribute.Wrapper);
    }

    [Theory]
    [InlineData("NoSeparator")]
    [InlineData(":LeadingSeparator")]
    [InlineData("TrailingSeparator:")]
    public void AMalformedTargetIsRejected(string target)
    {
        Assert.Throws<ArgumentException>(() => new PreHookAttribute(target));
    }

    [Fact]
    public void ATypedTargetKeepsTheWrapperForTheHostToResolve()
    {
        var attribute = new PostHookAttribute(typeof(UObject), "Foo") { IncludeOverrides = true };

        Assert.Equal(typeof(UObject), attribute.Wrapper);
        Assert.Null(attribute.ClassName);
        Assert.True(attribute.IncludeOverrides);
    }
}