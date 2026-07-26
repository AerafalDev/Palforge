namespace Palforge.Unreal.Reflection;

public sealed class UnrealDelegate
{
    private readonly UnrealValueBase _owner;
    private readonly int _offset;

    internal UnrealDelegate(UnrealValueBase owner, int offset)
    {
        _owner = owner;
        _offset = offset;
    }

    public bool Bind(UObject target, string functionName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        return _owner.Context.BindDelegate(_owner.Address + _offset, target.Address, functionName);
    }

    public bool Unbind()
    {
        return _owner.Context.ClearDelegate(_owner.Address + _offset);
    }

    public int Broadcast(params byte[][] arguments)
    {
        return _owner.Context.BroadcastDelegate(_owner.Address + _offset, arguments);
    }

    public override string ToString()
    {
        return _owner.Context.ScriptDelegate(_owner.Address + _offset);
    }
}