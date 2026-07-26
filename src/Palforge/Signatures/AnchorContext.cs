using Palforge.Images;
using Palforge.Memory;

namespace Palforge.Signatures;

internal readonly record struct AnchorContext(IMemory Memory, ModuleImage Image, nint Address)
{
    public bool IsInWritableSection()
    {
        return SectionAt(Address) is { IsExecutable: false };
    }

    public bool IsInExecutableSection()
    {
        return SectionAt(Address) is { IsExecutable: true };
    }

    private ImageSection? SectionAt(nint address)
    {
        foreach (var section in Image.Sections)
        {
            if (address >= section.Address && address < section.End)
                return section;
        }

        return null;
    }
}