using Palforge.Unreal.Sdk;

namespace Palforge.Tests.Unreal.Sdk;

public sealed class SdkNamingTests
{
    [Fact]
    public void APackageDirectoryBecomesADottedNamespaceUnderTheRoot()
    {
        Assert.Equal("Palforge.Sdk.Game.Pal.Blueprint", SdkNaming.Namespace("/Game/Pal/Blueprint"));
    }

    [Fact]
    public void AnEmptyDirectoryIsJustTheRootNamespace()
    {
        Assert.Equal("Palforge.Sdk", SdkNaming.Namespace(""));
        Assert.Equal("Palforge.Sdk", SdkNaming.Namespace("/"));
    }

    [Fact]
    public void UnderscoresAreDroppedAndSegmentsPascalCased()
    {
        Assert.Equal("ConsumStaminaPalThrow", SdkNaming.Identifier("ConsumStamina_PalThrow"));
        Assert.Equal("BpPalGameSettingC", SdkNaming.Identifier("BP_PalGameSetting_C"));
    }

    [Fact]
    public void AnAllCapsSegmentIsPascalCasedNotLeftShouting()
    {
        Assert.Equal("EPalPlayerSprintStaminaDecreaseTypeMax", SdkNaming.Identifier("EPalPlayerSprintStaminaDecreaseType_MAX"));
        Assert.Equal("Http", SdkNaming.Identifier("HTTP"));
    }

    [Fact]
    public void IllegalCharactersAndALeadingDigitAreSanitised()
    {
        Assert.Equal("_3WeirdName", SdkNaming.Identifier("3Weird-Name"));
        Assert.Equal("_", SdkNaming.Identifier(""));
    }

    [Fact]
    public void PascalCasingLiftsALowercaseKeywordClearOfNeedingAnEscape()
    {
        Assert.Equal("Object", SdkNaming.Identifier("object"));
        Assert.Equal("Event", SdkNaming.Identifier("event"));
    }

    [Fact]
    public void AParameterIsCamelCaseAndKeywordSafe()
    {
        Assert.Equal("bAsync", SdkNaming.Parameter("BAsync"));
        Assert.Equal("inString", SdkNaming.Parameter("InString"));
        Assert.Equal("@class", SdkNaming.Parameter("class"));
    }
}