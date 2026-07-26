using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Palforge.Signatures;

internal sealed class Pattern
{
    private const string XrefPrefix = "X0x";

    private readonly byte[] _values;
    private readonly byte[] _masks;
    private readonly PatternXref[] _xrefs;

    public string Text { get; }

    public int Cursor { get; }

    public int AnchorOffset { get; }

    public int AnchorLength { get; }

    public int Length =>
        _values.Length;

    public ReadOnlySpan<byte> Anchor =>
        _values.AsSpan(AnchorOffset, AnchorLength);

    private Pattern(string text, byte[] values, byte[] masks, PatternXref[] xrefs, int cursor, int anchorOffset, int anchorLength)
    {
        _values = values;
        _masks = masks;
        _xrefs = xrefs;

        Text = text;
        Cursor = cursor;
        AnchorOffset = anchorOffset;
        AnchorLength = anchorLength;
    }

    public static Pattern Parse(string text)
    {
        return TryParse(text, out var pattern)
            ? pattern
            : throw new FormatException($"Malformed pattern: '{text}'.");
    }

    public static Pattern Utf16(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);

        var bytes = Encoding.Unicode.GetBytes(literal);

        return FromBytes(bytes);
    }

    private static Pattern FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("A pattern needs at least one byte.", nameof(bytes));

        var builder = new StringBuilder(bytes.Length * 3);

        foreach (var value in bytes)
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture)).Append(' ');

        var masks = new byte[bytes.Length];

        masks.AsSpan().Fill(0xFF);

        return new Pattern(builder.ToString().TrimEnd(), bytes.ToArray(), masks, [], 0, 0, bytes.Length);
    }

    public static bool TryParse(string text, [NotNullWhen(true)] out Pattern? pattern)
    {
        pattern = null;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var values = new List<byte>(64);
        var masks = new List<byte>(64);
        var xrefs = new List<PatternXref>();

        var cursor = 0;

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token is "|")
            {
                cursor = values.Count;
                continue;
            }

            if (token.StartsWith(XrefPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!nint.TryParse(token.AsSpan(XrefPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var target))
                    return false;

                xrefs.Add(new PatternXref(values.Count, target));

                for (var index = 0; index < sizeof(int); index++)
                {
                    values.Add(0);
                    masks.Add(0);
                }

                continue;
            }

            if (!TryParseToken(token, out var value, out var mask))
                return false;

            values.Add(value);
            masks.Add(mask);
        }

        if (values.Count is 0)
            return false;

        var (anchorOffset, anchorLength) = LongestLiteralRun(masks);

        if (anchorLength is 0)
            return false;

        pattern = new Pattern(text, [.. values], [.. masks], [.. xrefs], cursor, anchorOffset, anchorLength);

        return true;
    }

    public bool Matches(ReadOnlySpan<byte> data, nint address)
    {
        if (data.Length < _values.Length)
            return false;

        for (var index = 0; index < _values.Length; index++)
        {
            if ((data[index] & _masks[index]) != _values[index])
                return false;
        }

        foreach (var xref in _xrefs)
        {
            var displacement = BinaryPrimitives.ReadInt32LittleEndian(data[xref.Offset..]);

            if (address + xref.Offset + sizeof(int) + displacement != xref.Target)
                return false;
        }

        return true;
    }

    private static bool TryParseToken(string token, out byte value, out byte mask)
    {
        value = 0;
        mask = 0;

        if (token is "?" or "??")
            return true;

        if (token.Length is 2 && token[1] is '?' && TryParseNibble(token[0], out var high))
        {
            value = (byte)(high << 4);
            mask = 0xF0;
            return true;
        }

        if (token.Length is 2 && token[0] is '?' && TryParseNibble(token[1], out var low))
        {
            value = low;
            mask = 0x0F;
            return true;
        }

        if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            return false;

        mask = 0xFF;
        return true;
    }

    private static bool TryParseNibble(char character, out byte nibble)
    {
        nibble = 0;

        if (!Uri.IsHexDigit(character))
            return false;

        nibble = (byte)Uri.FromHex(character);
        return true;
    }

    private static (int Offset, int Length) LongestLiteralRun(List<byte> masks)
    {
        var bestOffset = 0;
        var bestLength = 0;

        var offset = 0;
        var length = 0;

        for (var index = 0; index < masks.Count; index++)
        {
            if (masks[index] is not 0xFF)
            {
                length = 0;
                continue;
            }

            if (length is 0)
                offset = index;

            length++;

            if (length <= bestLength)
                continue;

            bestOffset = offset;
            bestLength = length;
        }

        return (bestOffset, bestLength);
    }
}