namespace Palforge.Unreal.Reflection;

public sealed class FMulticastSparseDelegateProperty : FProperty
{
    internal FMulticastSparseDelegateProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public bool IsBoundAt(nint container)
    {
        return Context.TryReadValue(container + Offset, out byte flags) && (flags & 1) is not 0;
    }

    public override string FormatValue(nint container)
    {
        if (!IsBoundAt(container))
            return "<sparse: unbound>";

        var list = Context.MulticastDelegateOf(Address, container + Offset);

        return list is not 0 ? Context.MulticastList(list) : "<sparse: bound>";
    }
}