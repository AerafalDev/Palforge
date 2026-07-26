namespace Palforge.Unreal.Reflection;

public sealed class FFieldClass
{
    public nint Address { get; }

    public string Name =>
        Context.FieldClassNameOf(Address);

    public EClassCastFlags CastFlags =>
        Context.FieldClassCastFlagsOf(Address);

    internal UnrealContext Context { get; }

    internal FFieldClass(nint address, UnrealContext context)
    {
        Address = address;
        Context = context;
    }

    public override string ToString()
    {
        return Name;
    }
}