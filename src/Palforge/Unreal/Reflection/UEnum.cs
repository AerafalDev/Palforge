namespace Palforge.Unreal.Reflection;

public class UEnum : UField
{
    public IEnumerable<(string Name, long Value)> Entries =>
        Context.EnumEntries(Address);

    internal UEnum(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string? GetNameByValue(long value)
    {
        foreach (var (name, entry) in Entries)
        {
            if (entry == value)
                return name;
        }

        return null;
    }

    public long? GetValueByName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        foreach (var (entry, value) in Entries)
        {
            if (entry == name)
                return value;
        }

        return null;
    }
}