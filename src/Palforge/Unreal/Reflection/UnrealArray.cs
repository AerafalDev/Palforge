using System.Collections;

namespace Palforge.Unreal.Reflection;

public sealed class UnrealArray<T> : IReadOnlyList<T>
{
    private readonly UnrealValueBase _owner;
    private readonly int _offset;
    private readonly int _stride;
    private readonly string _name;
    private readonly Func<UnrealContext, nint, T> _read;
    private readonly Func<UnrealContext, T, byte[]>? _write;
    private readonly Action<UnrealContext, byte[]>? _release;
    private FArrayProperty? _property;

    public int Count =>
        _owner.Context.TryReadArray(_owner.Address + _offset, out _, out var count) ? count : 0;

    public T this[int index]
    {
        get
        {
            if (index < 0 || !_owner.Context.TryReadArray(_owner.Address + _offset, out var data, out var count) || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _read(_owner.Context, data + (index * _stride));
        }
        set => WithElement(value, bytes => Property().SetElementAt(_owner.Address, index, bytes));
    }

    internal UnrealArray(UnrealValueBase owner, int offset, int stride, string name, Func<UnrealContext, nint, T> read, Func<UnrealContext, T, byte[]>? write, Action<UnrealContext, byte[]>? release)
    {
        _owner = owner;
        _offset = offset;
        _stride = stride;
        _name = name;
        _read = read;
        _write = write;
        _release = release;
    }

    public int Add(T value)
    {
        return WithElement(value, bytes => Property().AddAt(_owner.Address, bytes));
    }

    public int Insert(int index, T value)
    {
        return WithElement(value, bytes => Property().InsertAt(_owner.Address, index, bytes));
    }

    public bool RemoveAt(int index, int count = 1)
    {
        return Property().RemoveAt(_owner.Address, index, count);
    }

    public bool Clear()
    {
        return Property().ClearAt(_owner.Address);
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (!_owner.Context.TryReadArray(_owner.Address + _offset, out var data, out var count))
            yield break;

        for (var index = 0; index < count; index++)
            yield return _read(_owner.Context, data + (index * _stride));
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

    private FArrayProperty Property()
    {
        return _property ??= _owner.FindOwnProperty(_name) as FArrayProperty
            ?? throw new InvalidOperationException($"array property '{_name}' could not be resolved for mutation");
    }
}