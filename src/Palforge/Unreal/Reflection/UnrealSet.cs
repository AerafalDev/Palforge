using System.Collections;

namespace Palforge.Unreal.Reflection;

public sealed class UnrealSet<T> : IReadOnlyCollection<T>
{
    private readonly UnrealValueBase _owner;
    private readonly int _offset;
    private readonly int _stride;
    private readonly string _name;
    private readonly Func<UnrealContext, nint, T> _read;
    private readonly Func<UnrealContext, T, byte[]>? _write;
    private readonly Action<UnrealContext, byte[]>? _release;
    private FSetProperty? _property;

    public int Count =>
        _owner.Context.SparseCount(_owner.Address + _offset);

    internal UnrealSet(UnrealValueBase owner, int offset, int stride, string name, Func<UnrealContext, nint, T> read, Func<UnrealContext, T, byte[]>? write, Action<UnrealContext, byte[]>? release)
    {
        _owner = owner;
        _offset = offset;
        _stride = stride;
        _name = name;
        _read = read;
        _write = write;
        _release = release;
    }

    public bool Add(T value)
    {
        return WithElement(value, bytes => Property().AddAt(_owner.Address, bytes));
    }

    public bool Remove(T value)
    {
        return WithElement(value, bytes => Property().RemoveAt(_owner.Address, bytes));
    }

    public bool Contains(T value)
    {
        return WithElement(value, bytes => Property().ContainsAt(_owner.Address, bytes));
    }

    public bool Clear()
    {
        var emptied = true;

        foreach (var element in this.ToArray())
            emptied &= Remove(element);

        return emptied;
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var element in _owner.Context.SparseElements(_owner.Address + _offset, _stride))
            yield return _read(_owner.Context, element);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private TResult WithElement<TResult>(T value, Func<byte[], TResult> operation)
    {
        var bytes = _write is { } write
            ? write(_owner.Context, value)
            : throw new NotSupportedException($"mutating a '{_name}' element of type {typeof(T).Name} is not supported");

        try
        {
            return operation(bytes);
        }
        finally
        {
            _release?.Invoke(_owner.Context, bytes);
        }
    }

    private FSetProperty Property()
    {
        return _property ??= _owner.FindOwnProperty(_name) as FSetProperty
            ?? throw new InvalidOperationException($"set property '{_name}' could not be resolved for mutation");
    }
}