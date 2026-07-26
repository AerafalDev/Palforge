using System.Buffers;
using System.Diagnostics;
using Palforge.Memory;

namespace Palforge.Signatures;

internal static class PatternScanner
{
    private const int ChunkSize = 256 * 1024;

    public static List<nint> FindAll(ReadOnlySpan<byte> data, nint baseAddress, Pattern pattern)
    {
        var results = new List<nint>();

        FindAll(data, baseAddress, pattern, results, data.Length);

        return results;
    }

    public static ScanReport Scan(IMemory memory, nint address, int length, IReadOnlyList<Pattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(patterns);

        var matches = new List<nint>[patterns.Count];

        for (var index = 0; index < matches.Length; index++)
            matches[index] = [];

        if (patterns.Count is 0 || length <= 0)
            return new ScanReport(patterns, matches, TimeSpan.Zero);

        var longest = patterns.Max(static pattern => pattern.Length);
        var overlap = longest - 1;

        var timestamp = Stopwatch.GetTimestamp();

        if (length < longest)
            return new ScanReport(patterns, matches, Stopwatch.GetElapsedTime(timestamp));

        var chunks = (length - overlap + ChunkSize - 1) / ChunkSize;

        Parallel.For(0, Math.Max(chunks, 1), () => CreateBuckets(patterns.Count), (chunk, _, local) =>
        {
            var start = chunk * ChunkSize;
            var take = Math.Min(ChunkSize + overlap, length - start);

            if (take < longest)
                return local;

            var buffer = ArrayPool<byte>.Shared.Rent(take);

            try
            {
                var window = buffer.AsSpan(0, take);

                if (!memory.TryRead(address + start, window))
                    return local;

                var claimed = Math.Min(ChunkSize, take);

                for (var index = 0; index < patterns.Count; index++)
                    FindAll(window, address + start, patterns[index], local[index], claimed);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return local;
        }, local =>
        {
            lock (matches)
            {
                for (var index = 0; index < patterns.Count; index++)
                {
                    if (local[index] is { Count: > 0 } found)
                        matches[index].AddRange(found);
                }
            }
        });

        foreach (var match in matches)
            match.Sort();

        return new ScanReport(patterns, matches, Stopwatch.GetElapsedTime(timestamp));
    }

    private static List<nint>[] CreateBuckets(int count)
    {
        var buckets = new List<nint>[count];

        for (var index = 0; index < count; index++)
            buckets[index] = [];

        return buckets;
    }

    private static void FindAll(ReadOnlySpan<byte> data, nint baseAddress, Pattern pattern, List<nint> results, int claimed)
    {
        var offset = 0;

        while (offset <= data.Length - pattern.Length)
        {
            var window = data[offset..];
            var index = window.IndexOf(pattern.Anchor);

            if (index < 0)
                break;

            var start = index - pattern.AnchorOffset;

            if (start >= 0 && offset + start < claimed && start + pattern.Length <= window.Length && pattern.Matches(window[start..], baseAddress + offset + start))
                results.Add(baseAddress + offset + start + pattern.Cursor);

            offset += index + 1;
        }
    }
}