using Palforge.Images;
using Palforge.Memory;
using Palforge.Signatures.Anchors;

namespace Palforge.Signatures.Resolvers;

internal sealed class FNameConstructorResolver : LiteralXrefResolver
{
    private static readonly string[] s_literals = ["TGPUSkinVertexFactoryUnlimited\0", "MovementComponent0\0"];

    private static readonly string[] s_templates =
    [
        "48 8D 15 X0x{0} 48 8D 0D ?? ?? ?? ?? E8 | ?? ?? ?? ??",
        "41 B8 01 00 00 00 48 8D 15 X0x{0} 48 8D 0D ?? ?? ?? ?? E9 | ?? ?? ?? ??"
    ];

    protected override IReadOnlyList<string> Literals =>
        s_literals;

    protected override IReadOnlyList<string> Templates =>
        s_templates;

    protected override string UnverifiedReason =>
        "the FName constructor is not inside an executable section";

    public FNameConstructorResolver(IMemory memory, ModuleImage image) : base(memory, image)
    {
    }

    protected override Resolution<nint>? Prelude()
    {
        return new AnchorResolver(Memory, Image).Resolve(UnrealAnchors.FNameConstructor);
    }

    protected override bool TryProject(nint match, out nint target)
    {
        return Rip.TryResolve(Memory, match, out target);
    }
}