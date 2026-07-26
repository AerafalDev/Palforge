using Palforge.Images;
using Palforge.Memory;
using Palforge.Signatures;
using Palforge.Signatures.Anchors;
using Palforge.Signatures.Resolvers;

namespace Palforge.Unreal.Stage;

internal sealed class AnchorSet
{
    public required Resolution<EngineVersion> EngineVersion { get; init; }

    public required Resolution<nint> GUObjectArray { get; init; }

    public required Resolution<nint> FNameConstructor { get; init; }

    public required Resolution<nint> FNameToString { get; init; }

    public required Resolution<nint> GMalloc { get; init; }

    public required Resolution<nint> GameEngineTick { get; init; }

    public bool DerivationReady =>
        GUObjectArray.IsResolved && FNameToString.IsResolved;

    public static AnchorSet Resolve(IMemory memory, ModuleImage image)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(image);

        var anchors = new AnchorResolver(memory, image);

        return new AnchorSet
        {
            EngineVersion = new EngineVersionResolver(memory, image).Resolve(),
            GUObjectArray = anchors.Resolve(UnrealAnchors.GUObjectArray),
            FNameConstructor = new FNameConstructorResolver(memory, image).Resolve(),
            FNameToString = anchors.Resolve(UnrealAnchors.FNameToStringOut),
            GMalloc = anchors.Resolve(UnrealAnchors.GMalloc),
            GameEngineTick = new GameEngineTickResolver(memory, image).Resolve()
        };
    }
}