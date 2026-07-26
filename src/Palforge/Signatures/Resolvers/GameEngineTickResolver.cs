using Palforge.Images;
using Palforge.Memory;

namespace Palforge.Signatures.Resolvers;

internal sealed class GameEngineTickResolver : LiteralXrefResolver
{
    private static readonly string[] s_literals = ["causeevent=\0", "CAUSEEVENT \0"];

    private static readonly string[] s_templates =
    [
        "48 8D ?? X0x{0}",
        "4C 8D ?? X0x{0}"
    ];

    protected override IReadOnlyList<string> Literals =>
        s_literals;

    protected override IReadOnlyList<string> Templates =>
        s_templates;

    protected override string UnverifiedReason =>
        "UGameEngine::Tick is not inside an executable section";

    public GameEngineTickResolver(IMemory memory, ModuleImage image) : base(memory, image)
    {
    }

    protected override bool TryProject(nint match, out nint target)
    {
        return Image.TryGetFunctionStart(Memory, match, out target);
    }
}