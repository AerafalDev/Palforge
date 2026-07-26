namespace Palforge.Unreal.Reflection;

public sealed class UnrealMulticastDelegate
{
    private readonly UnrealValueBase _owner;
    private readonly int _offset;
    private readonly string _name;
    private readonly bool _sparse;
    private FProperty? _property;

    private nint Value =>
        _owner.Address + _offset;

    internal UnrealMulticastDelegate(UnrealValueBase owner, int offset, string name, bool sparse)
    {
        _owner = owner;
        _offset = offset;
        _name = name;
        _sparse = sparse;
    }

    public bool Add(UObject target, string functionName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        return _owner.Context.MulticastAdd(Header(), target.Address, functionName);
    }

    public bool Remove(UObject target, string functionName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        return _owner.Context.MulticastRemove(Header(), target.Address, functionName);
    }

    public bool Clear()
    {
        return _owner.Context.MulticastClear(Header());
    }

    public bool Contains(UObject target, string functionName)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Header() is var header && header is not 0 && _owner.Context.MulticastFind(header, target.Address, functionName) >= 0;
    }

    public int Broadcast(params byte[][] arguments)
    {
        return Header() is var header && header is not 0 ? _owner.Context.BroadcastMulticast(header, arguments) : 0;
    }

    public override string ToString()
    {
        return Header() is var header && header is not 0 ? _owner.Context.MulticastList(header) : "[]";
    }

    private nint Header()
    {
        return _sparse ? _owner.Context.MulticastDelegateOf(Property().Address, Value) : Value;
    }

    private FProperty Property()
    {
        return _property ??= _owner.FindOwnProperty(_name)
            ?? throw new InvalidOperationException($"delegate property '{_name}' could not be resolved");
    }
}