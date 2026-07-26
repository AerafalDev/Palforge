using System.Collections;

namespace Palforge.Unreal.Reflection;

public sealed class UnrealMap<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>>
{
    private readonly UnrealValueBase _owner;
    private readonly int _offset;
    private readonly int _stride;
    private readonly int _valueOffset;
    private readonly string _name;
    private readonly Func<UnrealContext, nint, TKey> _readKey;
    private readonly Func<UnrealContext, nint, TValue> _readValue;
    private readonly Func<UnrealContext, TKey, byte[]>? _writeKey;
    private readonly Func<UnrealContext, TValue, byte[]>? _writeValue;
    private readonly Action<UnrealContext, byte[]>? _releaseKey;
    private readonly Action<UnrealContext, byte[]>? _releaseValue;
    private FMapProperty? _property;

    public int Count =>
        _owner.Context.SparseCount(_owner.Address + _offset);

    public TValue this[TKey key] =>
        TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"the '{_name}' map has no such key");

    internal UnrealMap(UnrealValueBase owner, int offset, int stride, int valueOffset, string name, Func<UnrealContext, nint, TKey> readKey, Func<UnrealContext, nint, TValue> readValue, Func<UnrealContext, TKey, byte[]>? writeKey, Func<UnrealContext, TValue, byte[]>? writeValue, Action<UnrealContext, byte[]>? releaseKey, Action<UnrealContext, byte[]>? releaseValue)
    {
        _owner = owner;
        _offset = offset;
        _stride = stride;
        _valueOffset = valueOffset;
        _name = name;
        _readKey = readKey;
        _readValue = readValue;
        _writeKey = writeKey;
        _writeValue = writeValue;
        _releaseKey = releaseKey;
        _releaseValue = releaseValue;
    }

    public bool Add(TKey key, TValue value)
    {
        return WithKey(key, keyBytes => WithValue(value, valueBytes => Property().AddAt(_owner.Address, keyBytes, valueBytes)));
    }

    public bool Remove(TKey key)
    {
        return WithKey(key, bytes => Property().RemoveAt(_owner.Address, bytes));
    }

    public bool ContainsKey(TKey key)
    {
        return WithKey(key, bytes => Property().ContainsKeyAt(_owner.Address, bytes));
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        foreach (var pair in this)
        {
            if (EqualityComparer<TKey>.Default.Equals(pair.Key, key))
            {
                value = pair.Value;

                return true;
            }
        }

        value = default!;

        return false;
    }

    public bool Clear()
    {
        var emptied = true;

        foreach (var pair in this.ToArray())
            emptied &= Remove(pair.Key);

        return emptied;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var element in _owner.Context.SparseElements(_owner.Address + _offset, _stride))
            yield return new KeyValuePair<TKey, TValue>(_readKey(_owner.Context, element), _readValue(_owner.Context, element + _valueOffset));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private TResult WithKey<TResult>(TKey key, Func<byte[], TResult> operation)
    {
        return With(key, _writeKey, _releaseKey, "key", operation);
    }

    private TResult WithValue<TResult>(TValue value, Func<byte[], TResult> operation)
    {
        return With(value, _writeValue, _releaseValue, "value", operation);
    }

    private TResult With<TSource, TResult>(TSource source, Func<UnrealContext, TSource, byte[]>? write, Action<UnrealContext, byte[]>? release, string role, Func<byte[], TResult> operation)
    {
        var bytes = write is { } marshal
            ? marshal(_owner.Context, source)
            : throw new NotSupportedException($"mutating a '{_name}' {role} of type {typeof(TSource).Name} is not supported");

        try
        {
            return operation(bytes);
        }
        finally
        {
            release?.Invoke(_owner.Context, bytes);
        }
    }

    private FMapProperty Property()
    {
        return _property ??= _owner.FindOwnProperty(_name) as FMapProperty
            ?? throw new InvalidOperationException($"map property '{_name}' could not be resolved for mutation");
    }
}