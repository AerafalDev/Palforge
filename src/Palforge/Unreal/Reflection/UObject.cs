namespace Palforge.Unreal.Reflection;

public class UObject : UnrealValueBase
{
    public string Name =>
        Context.NameOf(Address);

    public int NameId =>
        Context.NameIdOf(Address);

    public UClass? Class =>
        Context.AsClass(Context.ClassPointerOf(Address));

    public UObject? Outer =>
        Context.WrapOrNull(Context.OuterPointerOf(Address));

    public EObjectFlags Flags =>
        Context.ObjectFlagsOf(Address);

    public bool IsDefaultObject =>
        (Flags & EObjectFlags.ClassDefaultObject) is not 0;

    public bool IsTemplate =>
        (Flags & (EObjectFlags.ClassDefaultObject | EObjectFlags.ArchetypeObject)) is not 0;

    internal UObject(nint address, UnrealContext context) : base(address, context)
    {
    }

    internal override FProperty? FindOwnProperty(string name)
    {
        return Class?.FindProperty(name);
    }

    public bool IsA(UClass type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Context.IsA(Address, type.Address);
    }

    public T Get<T>(FProperty property)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.Get<T>(this);
    }

    public T Get<T>(string propertyName)
        where T : unmanaged
    {
        return RequireProperty(propertyName).Get<T>(this);
    }

    public bool TryGet<T>(string propertyName, out T value)
        where T : unmanaged
    {
        var property = Class?.FindProperty(propertyName);

        if (property is null)
        {
            value = default;
            return false;
        }

        return property.TryGet(this, out value);
    }

    public bool TrySet<T>(string propertyName, in T value)
        where T : unmanaged
    {
        return Class?.FindProperty(propertyName) is { } property && property.TrySet(this, value);
    }

    public void Set<T>(FProperty property, in T value)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(property);

        property.Set(this, value);
    }

    public void Set<T>(string propertyName, in T value)
        where T : unmanaged
    {
        RequireProperty(propertyName).Set(this, value);
    }

    protected byte[]? Call(string functionName, params byte[][] arguments)
    {
        return Class?.FindFunction(functionName)?.Invoke(this, arguments);
    }

    protected byte[]? Call(string functionName, byte[][] arguments, out byte[][] outputs)
    {
        if (Class?.FindFunction(functionName) is { } function)
            return function.Invoke(this, arguments, out outputs);

        outputs = [];

        return null;
    }

    protected byte[]? Call(string functionName, byte[][] arguments, nint[] destinations, out byte[][] outputs)
    {
        if (Class?.FindFunction(functionName) is { } function)
            return function.Invoke(this, arguments, destinations, out outputs);

        outputs = [];

        return null;
    }

    private FProperty RequireProperty(string name)
    {
        return Class?.FindProperty(name) ?? throw new InvalidOperationException($"'{Name}' ({Class?.Name ?? "?"}) has no property '{name}'");
    }

    public override string ToString()
    {
        return $"{Class?.Name ?? "?"} {Name}";
    }
}