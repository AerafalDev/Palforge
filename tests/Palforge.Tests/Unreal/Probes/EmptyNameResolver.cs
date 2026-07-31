using Palforge.Unreal.Names;

namespace Palforge.Tests.Unreal.Probes;

internal sealed class EmptyNameResolver : INameResolver
{
    public bool TryFind(string name, out int id)
    {
        id = 0;

        return false;
    }

    public bool TryResolve(int id, out string name)
    {
        name = string.Empty;

        return false;
    }

    public bool TryResolve(int id, int number, out string name)
    {
        name = string.Empty;

        return false;
    }

    public bool TryIntern(string name, out long fname)
    {
        fname = 0;

        return false;
    }
}