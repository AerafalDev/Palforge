namespace Palforge.Unreal.Reflection;

public sealed class FLazyObjectProperty : FProperty
{
    internal FLazyObjectProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string GetGuid(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetGuidAt(target.Address);
    }

    public string GetGuidAt(nint container)
    {
        return Context.LazyGuid(container + Offset);
    }

    public override string FormatValue(nint container)
    {
        return GetGuidAt(container) is { Length: > 0 } guid ? guid : "None";
    }
}