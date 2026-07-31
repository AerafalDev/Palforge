using System.Numerics;

namespace Palforge.Commands.Arguments;

internal sealed class ArgumentReaderRegistry
{
    private readonly Dictionary<Type, ArgumentReader> _readers;
    private readonly Lock _gate;

    public ArgumentReaderRegistry()
    {
        _readers = [];
        _gate = new Lock();

        _readers[typeof(string)] = new StringArgumentReader();

        Add<bool>();
        Add<char>();
        Add<byte>();
        Add<sbyte>();
        Add<short>();
        Add<ushort>();
        Add<int>();
        Add<uint>();
        Add<long>();
        Add<ulong>();
        Add<Int128>();
        Add<UInt128>();
        Add<nint>();
        Add<nuint>();
        Add<float>();
        Add<double>();
        Add<decimal>();
        Add<Half>();
        Add<BigInteger>();
        Add<Guid>();
        Add<DateTime>();
        Add<DateTimeOffset>();
        Add<DateOnly>();
        Add<TimeOnly>();
        Add<TimeSpan>();
    }

    public ArgumentReader? Resolve(Type type)
    {
        lock (_gate)
            return ResolveCore(type);
    }

    public void Register(ArgumentReader reader)
    {
        lock (_gate)
            _readers[reader.TargetType] = reader;
    }

    private ArgumentReader? ResolveCore(Type type)
    {
        if (_readers.TryGetValue(type, out var existing))
            return existing;

        ArgumentReader? built = null;

        if (type.IsEnum)
            built = (ArgumentReader)Activator.CreateInstance(typeof(EnumArgumentReader<>).MakeGenericType(type))!;
        else if (Nullable.GetUnderlyingType(type) is { } underlying && ResolveCore(underlying) is { } inner)
            built = (ArgumentReader)Activator.CreateInstance(typeof(NullableArgumentReader<>).MakeGenericType(underlying), inner)!;

        if (built is not null)
            _readers[type] = built;

        return built;
    }

    private void Add<T>()
        where T : ISpanParsable<T>
    {
        _readers[typeof(T)] = new ParsableArgumentReader<T>();
    }
}