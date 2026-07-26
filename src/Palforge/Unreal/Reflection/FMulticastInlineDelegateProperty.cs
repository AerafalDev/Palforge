namespace Palforge.Unreal.Reflection;

public sealed class FMulticastInlineDelegateProperty : FProperty
{
    internal FMulticastInlineDelegateProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public override string FormatValue(nint container)
    {
        return Context.MulticastList(container + Offset);
    }
}