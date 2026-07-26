namespace Palforge.Unreal.Reflection;

public sealed class FSoftObjectProperty : FProperty
{
    internal FSoftObjectProperty(nint address, UnrealContext context) : base(address, context)
    {
    }

    public string GetPath(UObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GetPathAt(target.Address);
    }

    public string GetPathAt(nint container)
    {
        return Context.SoftObjectPath(container + Offset);
    }

    public bool SetPath(UObject target, string path)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetPathAt(target.Address, path);
    }

    public bool SetPathAt(nint container, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Context.WriteSoftObjectPath(container + Offset, path);
    }

    public override string FormatValue(nint container)
    {
        var path = GetPathAt(container);

        return path.Length is 0 ? "None" : path;
    }
}