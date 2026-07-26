namespace Palforge.Unreal.Reflection;

public sealed class FDelegateProperty : FProperty
{
    private const int FunctionNameOffset = 8;

    internal FDelegateProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public UObject? GetBoundObject(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetBoundObjectAt(target.Address);
    }

    public string GetFunctionName(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetFunctionNameAt(target.Address);
    }

    public UObject? GetBoundObjectAt(nint container)
    {
        return Context.WeakObject(container + Offset);
    }

    public string GetFunctionNameAt(nint container)
    {
        return Context.NameAtSlot(container + Offset + FunctionNameOffset);
    }

    public override string FormatValue(nint container)
    {
        return Context.ScriptDelegate(container + Offset);
    }
}