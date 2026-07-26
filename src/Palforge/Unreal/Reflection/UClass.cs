namespace Palforge.Unreal.Reflection;

public class UClass : UStruct
{
    public UClass? SuperClass =>
        Context.AsClass(Context.SuperPointerOf(Address));

    public UObject? ClassDefaultObject =>
        Context.WrapOrNull(Context.DefaultObjectPointerOf(Address));

    internal UClass(nint address, UnrealContext context) : base(address, context)
    {
    }
}