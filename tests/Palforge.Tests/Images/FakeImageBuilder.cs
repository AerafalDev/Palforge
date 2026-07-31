using System.Text;
using Palforge.Tests.Memory;

namespace Palforge.Tests.Images;

internal sealed class FakeImageBuilder
{
    private const int Lfanew = 0x80;
    private const int OptionalHeaderSize = 240;

    private readonly List<(string Name, int Address, int Size, uint Characteristics)> _sections = [];

    public ushort DosSignature { get; set; } = 0x5A4D;

    public int PeSignature { get; set; } = 0x00004550;

    public ushort Magic { get; set; } = 0x20B;

    public int ImageSize { get; set; } = 0x10000;

    public ushort SectionCountOverride { get; set; }

    public FakeImageBuilder WithSection(string name, int address, int size, uint characteristics)
    {
        _sections.Add((name, address, size, characteristics));

        return this;
    }

    public nint Build(FakeMemory memory)
    {
        var headerSize = Lfanew + 24 + OptionalHeaderSize + _sections.Count * 40 + 64;
        var address = memory.Allocate(Math.Max(headerSize, 0x1000));

        var headers = address + Lfanew;
        var optionalHeader = headers + 24;

        memory.TryWrite(address, DosSignature);
        memory.TryWrite(address + 0x3C, Lfanew);
        memory.TryWrite(headers, PeSignature);
        memory.TryWrite(headers + 6, (ushort)(SectionCountOverride is 0 ? _sections.Count : SectionCountOverride));
        memory.TryWrite(headers + 20, (ushort)OptionalHeaderSize);
        memory.TryWrite(optionalHeader, Magic);
        memory.TryWrite(optionalHeader + 56, ImageSize);

        var table = optionalHeader + OptionalHeaderSize;

        Span<byte> encoded = stackalloc byte[8];

        for (var index = 0; index < _sections.Count; index++)
        {
            var (name, sectionAddress, size, characteristics) = _sections[index];
            var header = table + index * 40;

            encoded.Clear();
            Encoding.ASCII.GetBytes(name).CopyTo(encoded);

            memory.TryWrite(header, encoded);
            memory.TryWrite(header + 8, size);
            memory.TryWrite(header + 12, sectionAddress);
            memory.TryWrite(header + 36, characteristics);
        }

        return address;
    }
}