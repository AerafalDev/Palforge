namespace Palforge.Unreal.Reflection;

public sealed class FInterfaceProperty : FObjectProperty
{
    internal FInterfaceProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public override string FormatValue(nint container)
    {
        return $"interface {base.FormatValue(container)}";
    }
}