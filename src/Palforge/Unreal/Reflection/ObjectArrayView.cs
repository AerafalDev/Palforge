using System.Diagnostics.CodeAnalysis;
using Palforge.Layout;
using Palforge.Memory;
using Palforge.Unreal.Probes;

namespace Palforge.Unreal.Reflection;

internal sealed class ObjectArrayView
{
    private readonly IMemory _memory;
    private readonly nint _table;
    private readonly nint _numElements;
    private readonly int _stride;
    private readonly int _itemObject;
    private readonly int _elementsPerChunk;
    private readonly int _itemSerialNumber;

    public int Count =>
        _memory.TryRead(_numElements, out int count) && count > 0 ? count : 0;

    public int InternalIndexOffset { get; }

    private ObjectArrayView(IMemory memory, nint table, nint numElements, int stride, int itemObject, int elementsPerChunk, int internalIndex, int itemSerialNumber)
    {
        _memory = memory;
        _table = table;
        _numElements = numElements;
        _stride = stride;
        _itemObject = itemObject;
        _elementsPerChunk = elementsPerChunk;
        _itemSerialNumber = itemSerialNumber;

        InternalIndexOffset = internalIndex;
    }

    public static bool TryCreate(IMemory memory, nint objectArray, LayoutTable layout, [NotNullWhen(true)] out ObjectArrayView? view)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(layout);

        view = null;

        if (!layout.TryGetOffset(LayoutNames.ObjectsTable, out var table)
            || !layout.TryGetOffset(LayoutNames.ItemStride, out var stride)
            || !layout.TryGetOffset(LayoutNames.ItemObject, out var itemObject)
            || !layout.TryGetOffset(LayoutNames.ElementsPerChunk, out var elementsPerChunk)
            || !layout.TryGetOffset(LayoutNames.NumElements, out var numElements)
            || !layout.TryGetOffset(LayoutNames.InternalIndex, out var internalIndex))
        {
            return false;
        }

        if (!memory.TryRead(objectArray + table, out nint chunks) || chunks is 0)
            return false;

        if (!memory.TryRead(objectArray + numElements, out int count) || count <= 0)
            return false;

        var itemSerialNumber = layout.TryGetOffset(LayoutNames.ItemSerialNumber, out var serial) ? serial : -1;

        view = new ObjectArrayView(memory, chunks, objectArray + numElements, stride, itemObject, elementsPerChunk, internalIndex, itemSerialNumber);

        return true;
    }

    public bool TryGetAddress(int index, out nint address)
    {
        address = 0;

        if ((uint)index >= (uint)Count)
            return false;

        var chunk = index / _elementsPerChunk;
        var slot = index % _elementsPerChunk;

        return _memory.TryRead(_table + (chunk * nint.Size), out nint items)
            && items is not 0
            && _memory.TryRead(items + (slot * _stride) + _itemObject, out address)
            && address is not 0;
    }

    public bool TryGetSerial(int index, out int serial)
    {
        serial = 0;

        if (_itemSerialNumber < 0 || (uint)index >= (uint)Count)
            return false;

        var chunk = index / _elementsPerChunk;
        var slot = index % _elementsPerChunk;

        return _memory.TryRead(_table + (chunk * nint.Size), out nint items)
            && items is not 0
            && _memory.TryRead(items + (slot * _stride) + _itemSerialNumber, out serial);
    }

    public IEnumerable<nint> Addresses()
    {
        return AddressesFrom(0);
    }

    public IEnumerable<nint> AddressesFrom(int start)
    {
        var count = Count;

        for (var index = Math.Max(0, start); index < count; index++)
        {
            if (TryGetAddress(index, out var address))
                yield return address;
        }
    }
}