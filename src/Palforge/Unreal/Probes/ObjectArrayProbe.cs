using Palforge.Layout;
using Palforge.Memory;
using Palforge.Signatures;

namespace Palforge.Unreal.Probes;

internal static class ObjectArrayProbe
{
    private const int ArrayScan = 0x80;
    private const int HeaderScan = 0x80;
    private const int SampleSlots = 16;
    private const int MinChunkSize = 16;
    private const int MaxChunkSize = 1 << 21;
    private const int MaxChunkCount = 8192;

    private static readonly int[] s_strides = [0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40];
    private static readonly int[] s_itemObjects = [0x00, 0x08];

    public static LayoutTable Probe(IMemory memory, nint objectArray)
    {
        ArgumentNullException.ThrowIfNull(memory);

        var solved = SolveArray(memory, objectArray);

        if (!solved.TryGetValue(out var solution))
            return Failed(solved);

        if (!TryReadTable(memory, objectArray + solution.ObjectsTable, out var table) || !memory.TryRead(table, out nint first) || first is 0)
            return Failed(solved, "the object array has no readable chunk");

        var (chunkSize, count) = SolveHeader(memory, objectArray, solution, table);

        return new LayoutTable(
        [
            LayoutMember.Derived(LayoutNames.ObjectsTable, solution.ObjectsTable),
            LayoutMember.Derived(LayoutNames.ItemStride, solution.ItemStride),
            LayoutMember.Derived(LayoutNames.ItemObject, solution.ItemObject),
            LayoutMember.Derived(LayoutNames.InternalIndex, solution.InternalIndex),
            chunkSize.TryGetValue(out var elementsPerChunk)
                ? LayoutMember.Derived(LayoutNames.ElementsPerChunk, elementsPerChunk)
                : LayoutMember.Undetermined(LayoutNames.ElementsPerChunk, chunkSize.Agreeing, $"{chunkSize}; ObjectsTable=0x{solution.ObjectsTable:X}; header {DumpHeader(memory, objectArray + solution.ObjectsTable)}"),
            count.TryGetValue(out var numElements)
                ? LayoutMember.Derived(LayoutNames.NumElements, numElements)
                : LayoutMember.Undetermined(LayoutNames.NumElements, count.Agreeing, $"{count}; ObjectsTable=0x{solution.ObjectsTable:X}; header {DumpHeader(memory, objectArray + solution.ObjectsTable)}")
        ]);
    }

    private static (Resolution<int> ChunkSize, Resolution<int> NumElements) SolveHeader(IMemory memory, nint objectArray, ArraySolution solution, nint table)
    {
        var chunkSizes = new List<Candidate<int>>();
        var counts = new List<Candidate<int>>();

        var structBase = objectArray + solution.ObjectsTable;

        for (var quad = 0; quad + (3 * sizeof(int)) <= HeaderScan; quad += sizeof(int))
        {
            if (!memory.TryRead(structBase + quad, out int maxElements) || maxElements <= 0)
                continue;

            if (!memory.TryRead(structBase + quad + (2 * sizeof(int)), out int maxChunks) || maxChunks is <= 0 or > MaxChunkCount)
                continue;

            if (!memory.TryRead(structBase + quad + (3 * sizeof(int)), out int numChunks) || numChunks <= 0 || numChunks > maxChunks)
                continue;

            if (maxElements % maxChunks is not 0)
                continue;

            var chunkSize = maxElements / maxChunks;

            if (!IsPowerOfTwo(chunkSize) || chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
                continue;

            if (!memory.TryRead(structBase + quad + sizeof(int), out int numElements) || numElements <= (long)(numChunks - 1) * chunkSize || numElements > (long)numChunks * chunkSize || numElements > maxElements)
                continue;

            if (!ChunkCountMatches(memory, table, numChunks) || !HighWaterHolds(memory, table, solution, numChunks, chunkSize, numElements))
                continue;

            chunkSizes.Add(new Candidate<int>(0, chunkSize));
            counts.Add(new Candidate<int>(0, solution.ObjectsTable + quad + sizeof(int)));
        }

        return (Unanimity.EnsureOne(chunkSizes, 1), Unanimity.EnsureOne(counts, 1));
    }

    private static bool ChunkCountMatches(IMemory memory, nint table, int numChunks)
    {
        for (var chunk = 0; chunk < numChunks; chunk++)
        {
            if (!memory.TryRead(table + (chunk * nint.Size), out nint address) || address is 0 || !memory.IsReadable(address, nint.Size))
                return false;
        }

        return !memory.TryRead(table + (numChunks * nint.Size), out nint beyond) || beyond is 0 || !memory.IsReadable(beyond, nint.Size);
    }

    private static bool HighWaterHolds(IMemory memory, nint table, ArraySolution solution, int chunkCount, int chunkSize, int numElements)
    {
        if (!TryObjectAt(memory, table, solution, chunkCount, chunkSize, numElements, out var beyond) || beyond is 0)
            return true;

        return !memory.TryRead(beyond + solution.InternalIndex, out int beyondIndex) || beyondIndex != numElements;
    }

    private static bool TryObjectAt(IMemory memory, nint table, ArraySolution solution, int chunkCount, int chunkSize, int index, out nint address)
    {
        address = 0;

        if (index < 0 || index / chunkSize >= chunkCount)
            return false;

        if (!memory.TryRead(table + (index / chunkSize * nint.Size), out nint items) || items is 0)
            return false;

        return memory.TryRead(items + (index % chunkSize * solution.ItemStride) + solution.ItemObject, out address);
    }

    private static string DumpHeader(IMemory memory, nint structBase)
    {
        var parts = new List<string>();

        for (var offset = 0; offset <= 0x30; offset += sizeof(int))
        {
            if (memory.TryRead(structBase + offset, out int value))
                parts.Add($"+{offset:X}={value}");
        }

        return string.Join(' ', parts);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) is 0;
    }

    private static Resolution<ArraySolution> SolveArray(IMemory memory, nint objectArray)
    {
        var candidates = new List<Candidate<ArraySolution>>();

        for (var table = 0; table <= ArrayScan; table += nint.Size)
        {
            if (!TryReadChunk(memory, objectArray + table, out var chunk))
                continue;

            foreach (var stride in s_strides)
            {
                foreach (var itemObject in s_itemObjects)
                {
                    if (!TrySampleObjects(memory, chunk, stride, itemObject, out var objects))
                        continue;

                    for (var index = 0; index <= HeaderScan; index += sizeof(int))
                    {
                        if (StoresItsOwnIndex(memory, objects, index))
                            candidates.Add(new Candidate<ArraySolution>(0, new ArraySolution(table, stride, itemObject, index)));
                    }
                }
            }
        }

        return Unanimity.EnsureOne(candidates, 1);
    }

    private static bool TryReadTable(IMemory memory, nint address, out nint table)
    {
        return memory.TryRead(address, out table) && table is not 0 && memory.IsReadable(table, nint.Size * 2);
    }

    private static bool TryReadChunk(IMemory memory, nint address, out nint chunk)
    {
        chunk = 0;

        return TryReadTable(memory, address, out var table)
            && memory.TryRead(table, out chunk)
            && chunk is not 0
            && memory.IsReadable(chunk, nint.Size);
    }

    private static bool TrySampleObjects(IMemory memory, nint chunk, int stride, int itemObject, out nint[] objects)
    {
        objects = new nint[SampleSlots];

        for (var slot = 0; slot < SampleSlots; slot++)
        {
            if (!memory.TryRead(chunk + slot * stride + itemObject, out nint address) || address is 0)
                return false;

            if (!memory.IsReadable(address, nint.Size))
                return false;

            for (var earlier = 0; earlier < slot; earlier++)
            {
                if (objects[earlier] == address)
                    return false;
            }

            objects[slot] = address;
        }

        return true;
    }

    private static bool StoresItsOwnIndex(IMemory memory, nint[] objects, int offset)
    {
        for (var slot = 0; slot < objects.Length; slot++)
        {
            if (!memory.TryRead(objects[slot] + offset, out int stored) || stored != slot)
                return false;
        }

        return true;
    }

    private static LayoutTable Failed(Resolution<ArraySolution> solved, string? extra = null)
    {
        var reason = extra ?? $"{solved} (searched the array header to 0x{ArrayScan:X}, strides {string.Join('/', s_strides.Select(static stride => $"0x{stride:X}"))}, object header to 0x{HeaderScan:X})";

        return new LayoutTable(
        [
            Unresolved(LayoutNames.ObjectsTable, solved, reason),
            Unresolved(LayoutNames.ItemStride, solved, reason),
            Unresolved(LayoutNames.ItemObject, solved, reason),
            Unresolved(LayoutNames.InternalIndex, solved, reason),
            LayoutMember.NotAttempted(LayoutNames.ElementsPerChunk, "the object array was not solved"),
            LayoutMember.NotAttempted(LayoutNames.NumElements, "the object array was not solved")
        ]);
    }

    private static LayoutMember Unresolved<T>(string name, Resolution<T> resolution, string? reason = null)
    {
        return resolution.Status is ResolutionStatus.NotFound
            ? LayoutMember.Undetermined(name, 0, reason ?? resolution.ToString())
            : LayoutMember.Undetermined(name, resolution.Agreeing, reason ?? resolution.ToString());
    }
}