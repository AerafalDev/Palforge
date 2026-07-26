namespace Palforge.Unreal.Runtime;

internal sealed class UnrealRuntimeOptions
{
    public const string SectionName = "Unreal";

    public required bool DumpObjects { get; set; }

    public required bool DumpActors { get; set; }

    public required bool GenerateSdk { get; set; }
}