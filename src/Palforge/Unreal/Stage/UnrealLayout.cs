using Palforge.Layout;
using Palforge.Unreal.Names;
using Palforge.Unreal.Reflection;

namespace Palforge.Unreal.Stage;

internal sealed class UnrealLayout
{
    public bool IsReady { get; }

    public DerivationStage FailedAt { get; }

    public string? FailureReason { get; }

    public string Fingerprint { get; }

    public IReadOnlyList<string> Conflicts { get; }

    public LayoutTable Layout =>
        field ?? throw NotReady();

    public ObjectArrayView Objects =>
        field ?? throw NotReady();

    public INameResolver Names =>
        field ?? throw NotReady();

    private UnrealLayout(bool ready, LayoutTable? layout, ObjectArrayView? objects, INameResolver? names, DerivationStage failedAt, string? reason, string fingerprint, IReadOnlyList<string> conflicts)
    {
        Layout = layout;
        Objects = objects;
        Names = names;
        IsReady = ready;
        FailedAt = failedAt;
        FailureReason = reason;
        Fingerprint = fingerprint;
        Conflicts = conflicts;
    }

    public static UnrealLayout Ready(LayoutTable layout, ObjectArrayView objects, INameResolver names, string fingerprint, IReadOnlyList<string> conflicts)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(names);

        return new UnrealLayout(true, layout, objects, names, DerivationStage.None, null, fingerprint, conflicts);
    }

    public static UnrealLayout Failed(DerivationStage stage, string reason)
    {
        return new UnrealLayout(false, null, null, null, stage, reason, string.Empty, []);
    }

    private InvalidOperationException NotReady()
    {
        return new InvalidOperationException($"The Unreal layout is not ready: derivation failed at {FailedAt}. {FailureReason}".TrimEnd());
    }
}