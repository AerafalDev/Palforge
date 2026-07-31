using System.Buffers.Binary;

namespace Palforge.Tests.Images;

internal static class PeFileMapper
{
    public static byte[] Map(string path)
    {
        var file = File.ReadAllBytes(path);

        var lfanew = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(0x3C));
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(lfanew + 6));
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(lfanew + 20));

        var optionalHeader = lfanew + 24;
        var imageSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(optionalHeader + 56));
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(optionalHeader + 60));

        var image = new byte[imageSize];

        file.AsSpan(0, Math.Min(headerSize, file.Length)).CopyTo(image);

        var table = optionalHeader + optionalHeaderSize;

        for (var index = 0; index < sectionCount; index++)
        {
            var header = table + index * 40;

            var virtualSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(header + 8));
            var virtualAddress = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(header + 12));
            var rawSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(header + 16));
            var rawOffset = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(header + 20));

            var take = Math.Min(rawSize, virtualSize);

            if (take <= 0 || rawOffset + take > file.Length || virtualAddress + take > image.Length)
                continue;

            file.AsSpan(rawOffset, take).CopyTo(image.AsSpan(virtualAddress));
        }

        return image;
    }
}