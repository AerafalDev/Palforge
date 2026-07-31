using Palforge.Unreal.Names;

namespace Palforge.Tests.Unreal.Probes;

internal sealed class FakeNameTable : INameResolver
{
    private readonly Dictionary<string, int> _ids = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> _names = [];

    public int Count =>
        _names.Count;

    public FakeNameTable()
    {
        Intern("None");
    }

    public int Intern(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_ids.TryGetValue(name, out var existing))
            return existing;

        var id = _names.Count;

        _names.Add(name);
        _ids[name] = id;

        return id;
    }

    public bool TryFind(string name, out int id)
    {
        return _ids.TryGetValue(name, out id);
    }

    public bool TryResolve(int id, out string name)
    {
        if ((uint)id >= (uint)_names.Count)
        {
            name = string.Empty;
            return false;
        }

        name = _names[id];
        return true;
    }

    public bool TryResolve(int id, int number, out string name)
    {
        if (!TryResolve(id, out name))
            return false;

        if (number is not 0)
            name = $"{name}_{number - 1}";

        return true;
    }

    public bool TryIntern(string name, out long fname)
    {
        fname = Intern(name);

        return true;
    }
}