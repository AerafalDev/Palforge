namespace Palforge.Tests.Unreal.Probes;

internal static class FakeLayoutValidation
{
    public static void EnsureCoherent(this FakeLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        EnsureNoOverlap("UObject", layout.ObjectSize,
        [
            ("vtable", 0, nint.Size),
            ("ObjectFlags", layout.ObjectFlags, sizeof(int)),
            ("InternalIndex", layout.InternalIndex, sizeof(int)),
            ("ClassPrivate", layout.ClassPrivate, nint.Size),
            ("NamePrivate", layout.NamePrivate, sizeof(long)),
            ("OuterPrivate", layout.OuterPrivate, nint.Size)
        ]);

        EnsureNoOverlap("FProperty", layout.PropertyBaseSize,
        [
            ("vtable", 0, nint.Size),
            ("FField.ClassPrivate", layout.FFieldClassPrivate, nint.Size),
            ("FField.Owner", layout.FFieldOwner, nint.Size),
            ("FField.Next", layout.FFieldNext, nint.Size),
            ("FField.NamePrivate", layout.FFieldNamePrivate, sizeof(long)),
            ("FField.Flags", layout.FFieldFlags, sizeof(int)),
            ("ArrayDim", layout.ArrayDim, sizeof(int)),
            ("ElementSize", layout.ElementSize, sizeof(int)),
            ("PropertyFlags", layout.PropertyFlags, sizeof(ulong)),
            ("RepIndex", layout.RepIndex, sizeof(ushort)),
            ("Offset_Internal", layout.OffsetInternal, sizeof(int)),
            ("PropertyLinkNext", layout.PropertyLinkNext, nint.Size)
        ]);

        EnsureNoOverlap("FUObjectItem", layout.ItemStride,
        [
            ("Object", layout.ItemObject, nint.Size),
            ("Flags", layout.ItemFlags, sizeof(int)),
            ("SerialNumber", layout.ItemSerialNumber, sizeof(int))
        ]);
    }

    private static void EnsureNoOverlap(string owner, int size, (string Name, int Offset, int Width)[] members)
    {
        foreach (var (name, offset, width) in members)
        {
            if (offset < 0 || offset + width > size)
                throw new ArgumentException($"{owner}.{name} at 0x{offset:X} ({width} bytes) does not fit in 0x{size:X}.");
        }

        for (var left = 0; left < members.Length; left++)
        {
            for (var right = left + 1; right < members.Length; right++)
            {
                var a = members[left];
                var b = members[right];

                if (a.Offset < b.Offset + b.Width && b.Offset < a.Offset + a.Width)
                    throw new ArgumentException($"{owner}.{a.Name} at 0x{a.Offset:X} overlaps {owner}.{b.Name} at 0x{b.Offset:X}.");
            }
        }
    }
}