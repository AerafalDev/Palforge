namespace Palforge.Unreal.Names;

internal interface INameResolver
{
    bool TryFind(string name, out int id);

    bool TryResolve(int id, out string name);

    bool TryResolve(int id, int number, out string name);

    bool TryIntern(string name, out long fname);
}