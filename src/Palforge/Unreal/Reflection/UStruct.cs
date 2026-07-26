namespace Palforge.Unreal.Reflection;

public class UStruct : UField
{
    public UStruct? Super =>
        Context.AsStruct(Context.SuperPointerOf(Address));

    public IEnumerable<FProperty> Properties =>
        Context.PropertiesOf(Address);

    public IEnumerable<FProperty> AllProperties
    {
        get
        {
            for (var current = this; current is not null; current = current.Super)
            {
                foreach (var property in current.Properties)
                    yield return property;
            }
        }
    }

    public IEnumerable<UFunction> Functions =>
        Context.FunctionsOf(Address);

    internal UStruct(nint address, UnrealContext context): base(address, context)
    {
    }

    public FProperty? FindProperty(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        for (var current = this; current is not null; current = current.Super)
        {
            foreach (var property in current.Properties)
            {
                if (property.Name == name)
                    return property;
            }
        }

        return null;
    }

    public UFunction? FindFunction(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        for (var current = this; current is not null; current = current.Super)
        {
            foreach (var function in current.Functions)
            {
                if (function.Name == name)
                    return function;
            }
        }

        return null;
    }
}