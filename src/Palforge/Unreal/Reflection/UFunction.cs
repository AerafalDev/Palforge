namespace Palforge.Unreal.Reflection;

public class UFunction : UStruct
{
    public IEnumerable<FProperty> Parameters =>
        Properties.Where(static property => property.Flags.HasFlag(EPropertyFlags.Parm) && !property.Flags.HasFlag(EPropertyFlags.ReturnParm));

    public FProperty? ReturnParameter =>
        Properties.FirstOrDefault(static property => property.Flags.HasFlag(EPropertyFlags.ReturnParm));

    public EFunctionFlags FunctionFlags =>
        Context.FunctionFlagsOf(Address);

    public bool IsStatic =>
        (FunctionFlags & EFunctionFlags.Static) is not 0;

    internal UFunction(nint address, UnrealContext context) : base(address, context)
    {
    }

    public byte[]? Invoke(UObject target, params byte[][] args)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);

        return Context.InvokeFunction(target.Address, Address, args);
    }

    public byte[]? Invoke(UObject target, IReadOnlyList<byte[]> args, out byte[][] outputs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);

        return Context.InvokeFunction(target.Address, Address, args, out outputs);
    }

    public byte[]? Invoke(UObject target, IReadOnlyList<byte[]> args, IReadOnlyList<nint> destinations, out byte[][] outputs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);

        return Context.InvokeFunction(target.Address, Address, args, destinations, out outputs);
    }

    public byte[]? Invoke(UObject target, IReadOnlyList<byte[]> args, IReadOnlyList<nint>? destinations, nint returnDestination, out byte[][] outputs)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);

        return Context.InvokeFunction(target.Address, Address, args, destinations, returnDestination, out outputs);
    }
}