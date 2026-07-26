using System.Diagnostics.CodeAnalysis;
using System.Text;
using Palforge.Memory;

namespace Palforge.Images;

internal sealed class ModuleImage
{
    private const int DosSignature = 0x5A4D;
    private const int PeSignature = 0x00004550;
    private const int Pe32Plus = 0x20B;
    private const int LfanewOffset = 0x3C;
    private const int SectionHeaderSize = 40;
    private const int MaxSections = 96;

    private const int ExceptionDirectory = 136;
    private const int RuntimeFunctionSize = 12;
    private const int ChainInfoFlag = 0x04;
    private const int MaxChainDepth = 32;

    private const uint ContainsCode = 0x0000_0020;
    private const uint MemoryExecute = 0x2000_0000;
    private const uint MemoryRead = 0x4000_0000;

    public nint BaseAddress { get; }

    public int Size { get; }

    public IReadOnlyList<ImageSection> Sections { get; }

    public nint FunctionTable { get; private init; }

    public int FunctionCount { get; private init; }

    public IEnumerable<ImageSection> ExecutableSections =>
        Sections.Where(static section => section.IsExecutable);

    public IEnumerable<ImageSection> ReadableSections =>
        Sections.Where(static section => section.IsReadable);

    private ModuleImage(nint baseAddress, int size, IReadOnlyList<ImageSection> sections)
    {
        BaseAddress = baseAddress;
        Size = size;
        Sections = sections;
    }

    public static bool TryParse(IMemory memory, nint baseAddress, [NotNullWhen(true)] out ModuleImage? image)
    {
        ArgumentNullException.ThrowIfNull(memory);

        image = null;

        if (!memory.TryRead(baseAddress, out ushort dos) || dos is not DosSignature)
            return false;

        if (!memory.TryRead(baseAddress + LfanewOffset, out int lfanew) || lfanew <= 0 || lfanew > 0x1000)
            return false;

        var headers = baseAddress + lfanew;

        if (!memory.TryRead(headers, out int signature) || signature is not PeSignature)
            return false;

        if (!memory.TryRead(headers + 6, out ushort sectionCount) || sectionCount is 0 or > MaxSections)
            return false;

        if (!memory.TryRead(headers + 20, out ushort optionalHeaderSize) || optionalHeaderSize is 0)
            return false;

        var optionalHeader = headers + 24;

        if (!memory.TryRead(optionalHeader, out ushort magic) || magic is not Pe32Plus)
            return false;

        if (!memory.TryRead(optionalHeader + 56, out int imageSize) || imageSize <= 0)
            return false;

        var sections = new List<ImageSection>(sectionCount);
        var table = optionalHeader + optionalHeaderSize;

        for (var index = 0; index < sectionCount; index++)
        {
            if (!TryParseSection(memory, baseAddress, table + index * SectionHeaderSize, out var section))
                return false;

            sections.Add(section);
        }

        memory.TryRead(optionalHeader + ExceptionDirectory, out int exceptionRva);
        memory.TryRead(optionalHeader + ExceptionDirectory + 4, out int exceptionSize);

        var hasExceptions = exceptionRva > 0 && exceptionSize >= RuntimeFunctionSize;

        image = new ModuleImage(baseAddress, imageSize, sections)
        {
            FunctionTable = hasExceptions ? baseAddress + exceptionRva : 0,
            FunctionCount = hasExceptions ? exceptionSize / RuntimeFunctionSize : 0
        };

        return true;
    }

    public bool TryGetSection(string name, out ImageSection section)
    {
        foreach (var candidate in Sections)
        {
            if (!string.Equals(candidate.Name, name, StringComparison.Ordinal))
                continue;

            section = candidate;
            return true;
        }

        section = default;
        return false;
    }

    public bool TryGetFunctionStart(IMemory memory, nint address, out nint start)
    {
        ArgumentNullException.ThrowIfNull(memory);

        start = 0;

        if (FunctionCount is 0 || address < BaseAddress)
            return false;

        var rva = (uint)(address - BaseAddress);

        var low = 0;
        var high = FunctionCount - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var entry = FunctionTable + middle * RuntimeFunctionSize;

            if (!memory.TryRead(entry, out uint begin) || !memory.TryRead(entry + 4, out uint end))
                return false;

            if (rva < begin)
            {
                high = middle - 1;
                continue;
            }

            if (rva >= end)
            {
                low = middle + 1;
                continue;
            }

            return TryFollowChain(memory, entry, out start);
        }

        return false;
    }

    private bool TryFollowChain(IMemory memory, nint entry, out nint start)
    {
        start = 0;

        for (var depth = 0; depth < MaxChainDepth; depth++)
        {
            if (!memory.TryRead(entry, out uint begin) || !memory.TryRead(entry + 8, out uint unwindRva))
                return false;

            start = BaseAddress + (nint)begin;

            if (unwindRva is 0)
                return true;

            var unwind = BaseAddress + (nint)unwindRva;

            if (!memory.TryRead(unwind, out byte versionAndFlags) || !memory.TryRead(unwind + 2, out byte codeCount))
                return true;

            if ((versionAndFlags >> 3 & ChainInfoFlag) is 0)
                return true;

            entry = unwind + 4 + 2 * ((codeCount + 1) & ~1);
        }

        return true;
    }

    private static bool TryParseSection(IMemory memory, nint baseAddress, nint header, out ImageSection section)
    {
        section = default;

        Span<byte> name = stackalloc byte[8];

        if (!memory.TryRead(header, name))
            return false;

        if (!memory.TryRead(header + 8, out int virtualSize))
            return false;

        if (!memory.TryRead(header + 12, out int virtualAddress))
            return false;

        if (!memory.TryRead(header + 36, out uint characteristics))
            return false;

        if (virtualAddress < 0 || virtualSize < 0)
            return false;

        var executable = (characteristics & (MemoryExecute | ContainsCode)) is not 0;
        var readable = (characteristics & MemoryRead) is not 0;

        section = new ImageSection(Encoding.ASCII.GetString(name).TrimEnd('\0'), baseAddress + virtualAddress, virtualSize, executable, readable);
        return true;
    }
}