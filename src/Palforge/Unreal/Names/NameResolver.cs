using System.Globalization;
using System.Text;
using Palforge.Memory;

namespace Palforge.Unreal.Names;

internal sealed class NameResolver : INameResolver
{
    private const int FNameSize = 8;
    private const int FStringSize = 16;
    private const int MaxNameChars = 1024;
    private const int RequiredScratch = FNameSize + FStringSize + (MaxNameChars + 1) * 2;
    private const string None = "None";

    private readonly IMemory _memory;
    private readonly IFNameNatives _natives;
    private readonly nint _fnameSlot;
    private readonly nint _fstringSlot;
    private readonly nint _nameBuffer;
    private readonly Dictionary<string, int> _byName;
    private readonly Dictionary<int, string> _byId;
    private readonly Lock _gate;

    private Func<int>? _rebound;

    public int MaxComparisonIndex { get; set; }

    public Func<int>? Rebounding
    {
        get => _rebound;
        set => _rebound = value;
    }

    public NameResolver(IMemory memory, IFNameNatives natives, nint scratch, int scratchSize)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(natives);
        ArgumentOutOfRangeException.ThrowIfLessThan(scratchSize, RequiredScratch);

        _memory = memory;
        _natives = natives;
        _fnameSlot = scratch;
        _fstringSlot = scratch + FNameSize;
        _nameBuffer = scratch + FNameSize + FStringSize;
        _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        _byId = new Dictionary<int, string>();
        _gate = new Lock();

        MaxComparisonIndex = int.MaxValue;
    }

    public bool TryFind(string name, out int id)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            if (_byName.TryGetValue(name, out id))
                return true;

            if (name.Length is 0 or > MaxNameChars || !WriteWide(name))
            {
                id = 0;
                return false;
            }

            _memory.TryWrite(_fnameSlot, 0L);
            _natives.Construct(_fnameSlot, _nameBuffer, (int)EFindName.Find);

            if (!_memory.TryRead(_fnameSlot, out int index))
            {
                id = 0;
                return false;
            }

            if (index is 0 && name != None)
            {
                id = 0;
                return false;
            }

            Cache(index, name);

            id = index;
            return true;
        }
    }

    public bool TryIntern(string name, out long fname)
    {
        ArgumentNullException.ThrowIfNull(name);

        fname = 0;

        if (name.Length is 0 or > MaxNameChars)
            return false;

        lock (_gate)
        {
            if (!WriteWide(name))
                return false;

            _memory.TryWrite(_fnameSlot, 0L);
            _natives.Construct(_fnameSlot, _nameBuffer, (int)EFindName.Add);

            if (!_memory.TryRead(_fnameSlot, out fname))
                return false;

            var index = (int)fname;

            if (index > MaxComparisonIndex)
                MaxComparisonIndex = index;

            return true;
        }
    }

    public bool TryResolve(int id, int number, out string name)
    {
        if (!TryResolve(id, out var pooled))
        {
            name = string.Empty;
            return false;
        }

        name = number is 0 ? pooled : string.Create(CultureInfo.InvariantCulture, $"{pooled}_{number - 1}");
        return true;
    }

    public bool TryResolve(int id, out string name)
    {
        name = string.Empty;

        if (id < 0)
            return false;

        lock (_gate)
        {
            if (id > MaxComparisonIndex && !Rebound(id))
                return false;

            if (_byId.TryGetValue(id, out name!))
                return true;

            _memory.TryWrite(_fnameSlot, id);
            _memory.TryWrite(_fnameSlot + 4, 0);
            _memory.TryWrite(_fstringSlot, (nint)0);
            _memory.TryWrite(_fstringSlot + nint.Size, 0L);

            _natives.ToString(_fnameSlot, _fstringSlot);

            if (!TryReadString(out name))
                return false;

            Cache(id, name);
            return true;
        }
    }

    private bool Rebound(int id)
    {
        if (_rebound is not { } rescan)
            return false;

        MaxComparisonIndex = Math.Max(MaxComparisonIndex, rescan());

        return id <= MaxComparisonIndex;
    }

    private bool TryReadString(out string name)
    {
        name = string.Empty;

        if (!_memory.TryRead(_fstringSlot, out nint data) || !_memory.TryRead(_fstringSlot + nint.Size, out int count))
            return false;

        if (data is 0 || count <= 1)
            return true;

        var chars = Math.Min(count - 1, MaxNameChars);

        Span<byte> buffer = stackalloc byte[chars * 2];

        if (!_memory.TryRead(data, buffer))
            return false;

        name = Encoding.Unicode.GetString(buffer);
        return true;
    }

    private bool WriteWide(string name)
    {
        Span<byte> buffer = stackalloc byte[(name.Length + 1) * 2];

        Encoding.Unicode.GetBytes(name, buffer);

        buffer[^2] = 0;
        buffer[^1] = 0;

        return _memory.TryWrite(_nameBuffer, buffer);
    }

    private void Cache(int id, string name)
    {
        _byName[name] = id;
        _byId.TryAdd(id, name);
    }
}