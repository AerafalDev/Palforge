namespace Palforge.Unreal.Reflection;

public class FField
{
    public nint Address { get; }

    public string Name =>
        Context.FieldNameOf(Address);

    public FFieldClass? Class =>
        Context.WrapFieldClass(Context.FieldClassOf(Address));

    public FField? Next =>
        Context.WrapField(Context.FieldNextOf(Address));

    internal UnrealContext Context { get; }

    internal FField(nint address, UnrealContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Address = address;
        Context = context;
    }

    public override string ToString()
    {
        return $"{Class?.Name ?? "FField"} {Name}";
    }
}