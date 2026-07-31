using Palforge.Unreal.Sdk;

namespace Palforge.Tests.Unreal.Sdk;

public sealed class SdkEmitterTests
{
    [Fact]
    public void AnEnumEmitsItsNamespaceNameAndMembers()
    {
        var code = SdkEmitter.Emit(new SdkEnum("Palforge.Sdk.Engine", "EMovementMode",
            [new SdkEnumMember("Walking", 0), new SdkEnumMember("Falling", 3)]));

        Assert.Contains("namespace Palforge.Sdk.Engine;", code);
        Assert.Contains("public enum EMovementMode", code);
        Assert.Contains("    Walking = 0,", code);
        Assert.Contains("    Falling = 3", code);
        Assert.DoesNotContain("Falling = 3,", code);
    }

    [Fact]
    public void AClassEmitsTheWrappingCtorAndTypedProperties()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "AActor", "UObject",
        [
            new SdkProperty("Health", "int", "ReadAt<int>(0x1F0)", "WriteAt(0x1F0, value)"),
            new SdkProperty("Tag", "string", "ReadNameAt(0x28)", null)
        ], []));

        Assert.Contains("using Palforge.Unreal.Reflection;", code);
        Assert.Contains("public class AActor : UObject", code);
        Assert.Contains("internal AActor(nint address, UnrealContext context) : base(address, context)", code);

        Assert.Contains("public int Health", code);
        Assert.Contains("        get => ReadAt<int>(0x1F0);", code);
        Assert.Contains("        set => WriteAt(0x1F0, value);", code);

        Assert.Contains("public string Tag =>\n        ReadNameAt(0x28);", code);
        Assert.Equal(1, code.Split("set =>").Length - 1);

        Assert.True(code.IndexOf("public int Health", StringComparison.Ordinal) < code.IndexOf("internal AActor", StringComparison.Ordinal));
    }

    [Fact]
    public void AClassEmitsInstanceAndStaticMethods()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "MathLibrary", "UObject", [],
        [
            new SdkMethod("Add", "Add_IntInt", "MathLibrary", true,
                [new SdkParameter("A", "int", "Bytes(A)"), new SdkParameter("B", "int", "Bytes(B)")],
                "int", "As<int>(#)"),
            new SdkMethod("Reset", "Reset", "MathLibrary", false, [], "void", null)
        ]));

        Assert.Contains("using Palforge.Unreal.Sdk;", code);
        Assert.Contains("public static int Add(int A, int B)", code);
        Assert.Contains("        return As<int>(SdkEnv.CallStatic(\"MathLibrary\", \"Add_IntInt\", Bytes(A), Bytes(B)));", code);

        Assert.Contains("public void Reset()", code);
        Assert.Contains("        Call(\"Reset\");", code);
        Assert.DoesNotContain("=>", code);
    }

    [Fact]
    public void AClassEmitsStaticClassResolvingItsUeNameAfterTheConstructor()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "PalPlayer", "UObject", [], [], "BP_PalPlayer_C"));

        Assert.Contains("using Palforge.Unreal.Sdk;", code);
        Assert.Contains("public static UClass? StaticClass()", code);
        Assert.Contains("        return SdkEnv.StaticClass(\"BP_PalPlayer_C\");", code);
        Assert.True(code.IndexOf("internal PalPlayer", StringComparison.Ordinal) < code.IndexOf("StaticClass", StringComparison.Ordinal));
    }

    [Fact]
    public void AStaticClassOnADerivedClassHidesTheInheritedOneWithNew()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Texture2D", "Texture", [], [], "Texture2D"));

        Assert.Contains("public static new UClass? StaticClass()", code);
    }

    [Fact]
    public void AnActorClassEmitsSpawnAndANonActorEmitsNew()
    {
        var actor = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "PalPlayer", "UObject", [], [], "BP_PalPlayer_C", IsActor: true));
        var data = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "DataTable", "UObject", [], [], "DataTable", IsActor: false));

        Assert.Contains("public static PalPlayer? Spawn(UObject? owner = null, UStructValue? at = null)", actor);
        Assert.Contains("        return SdkEnv.SpawnActor(\"BP_PalPlayer_C\", owner, at) as PalPlayer;", actor);
        Assert.DoesNotContain(" New(", actor);

        Assert.Contains("public static DataTable? New(UObject? outer = null)", data);
        Assert.Contains("        return SdkEnv.New(\"DataTable\", outer) as DataTable;", data);
        Assert.DoesNotContain(" Spawn(", data);
    }

    [Fact]
    public void ADerivedActorHidesSpawnWithNew()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Pawn", "Actor", [], [], "Pawn", IsActor: true));

        Assert.Contains("public static new Pawn? Spawn(UObject? owner = null, UStructValue? at = null)", code);
    }

    [Fact]
    public void AStructWrapperHasNoStaticClass()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Vector", "UStructValue", [], []));

        Assert.DoesNotContain("StaticClass", code);
    }

    [Fact]
    public void AStructEmitsAllocateInsteadOfAConstructionHelper()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Vector", "UStructValue", [], [], "Vector", IsStruct: true));

        Assert.Contains("public static Vector? Allocate()", code);
        Assert.Contains("        return SdkEnv.AllocateStruct<Vector>(\"Vector\");", code);
        Assert.DoesNotContain("StaticClass", code);
        Assert.DoesNotContain("Spawn(", code);
    }

    [Fact]
    public void AStructCarriesItsEngineNameToTheBase()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Vector", "UStructValue", [], [], "Vector", IsStruct: true));

        Assert.Contains("internal Vector(nint address, UnrealContext context) : base(address, context, \"Vector\")", code);

        Assert.Contains("private protected Vector(nint address, UnrealContext context, string? structName) : base(address, context, structName)", code);
    }

    [Fact]
    public void AClassDoesNotCarryAName()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Actor", "UObject", [], [], "Actor"));

        Assert.Contains("internal Actor(nint address, UnrealContext context) : base(address, context)", code);
        Assert.DoesNotContain("string? structName", code);
    }

    [Fact]
    public void ADerivedStructHidesAllocateWithNew()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Vector2D", "Vector", [], [], "Vector2D", IsStruct: true));

        Assert.Contains("public static new Vector2D? Allocate()", code);
    }

    [Fact]
    public void AnOutParameterReadsBackFromTheCapturedSlot()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "World", "UObject", [],
        [
            new SdkMethod("Trace", "LineTrace", "World", false,
            [
                new SdkParameter("Start", "int", "Bytes(Start)"),
                new SdkParameter("Hit", "UObject?", "Bytes<nint>(0)", "out", "SdkEnv.Wrap(#)")
            ], "bool", "As<byte>(#) is not 0")
        ]));

        Assert.Contains("public bool Trace(int Start, out UObject? Hit)", code);
        Assert.Contains("        var arguments = new byte[][] { Bytes(Start), Bytes<nint>(0) };", code);
        Assert.Contains("        var result = Call(\"LineTrace\", arguments, out var outputs);", code);
        Assert.Contains("        Hit = SdkEnv.Wrap(outputs[1]);", code);
        Assert.Contains("        return As<byte>(result) is not 0;", code);
    }

    [Fact]
    public void AStructOutParameterPassesItsAddressAsTheCallDestination()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Actor", "UObject", [],
        [
            new SdkMethod("GetBounds", "GetActorBounds", "Actor", false,
            [
                new SdkParameter("OnlyColliding", "bool", "Bytes<byte>(0)"),
                new SdkParameter("Origin", "Vector", "SdkEnv.StructBytes(Origin, 24)", Destination: "Origin.Address")
            ], "void", null)
        ]));

        Assert.Contains("public void GetBounds(bool OnlyColliding, Vector Origin)", code);
        Assert.Contains("        var destinations = new nint[] { 0, Origin.Address };", code);
        Assert.Contains("        Call(\"GetActorBounds\", arguments, destinations, out var outputs);", code);
    }

    [Fact]
    public void AStructReturnIsAllocatedAndFilledRatherThanRead()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Actor", "UObject", [],
        [
            new SdkMethod("GetLocation", "K2_GetActorLocation", "Actor", false, [], "Vector", null, "Vector")
        ]));

        Assert.Contains("public Vector? GetLocation()", code);
        Assert.Contains("        return SdkEnv.CallForStruct<Vector>(this, \"K2_GetActorLocation\", \"Vector\");", code);
    }

    [Fact]
    public void AStaticStructReturnDispatchesOnItsClass()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "KismetMathLibrary", "UObject", [],
        [
            new SdkMethod("MakeVector", "MakeVector", "KismetMathLibrary", true,
                [new SdkParameter("X", "double", "Bytes(X)")], "Vector", null, "Vector")
        ]));

        Assert.Contains("public static Vector? MakeVector(double X)", code);
        Assert.Contains("        return SdkEnv.CallStaticForStruct<Vector>(\"KismetMathLibrary\", \"MakeVector\", \"Vector\", Bytes(X));", code);
    }

    [Fact]
    public void ARefParameterOnAStaticVoidMethodMarshalsInAndReadsBack()
    {
        var code = SdkEmitter.Emit(new SdkClass("Palforge.Sdk.Engine", "Lib", "UObject", [],
        [
            new SdkMethod("Bump", "Bump", "Lib", true,
                [new SdkParameter("Value", "int", "Bytes(Value)", "ref", "As<int>(#)")], "void", null)
        ]));

        Assert.Contains("public static void Bump(ref int Value)", code);
        Assert.Contains("        var arguments = new byte[][] { Bytes(Value) };", code);
        Assert.Contains("        SdkEnv.CallStatic(\"Lib\", \"Bump\", arguments, out var outputs);", code);
        Assert.Contains("        Value = As<int>(outputs[0]);", code);
    }
}