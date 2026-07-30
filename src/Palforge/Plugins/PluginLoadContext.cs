using System.Reflection;
using System.Runtime.Loader;

namespace Palforge.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly AssemblyLoadContext s_host = GetLoadContext(typeof(PluginLoadContext).Assembly) ?? Default;

    private readonly AssemblyDependencyResolver _resolver;

    internal PluginLoadContext(string name, string assemblyPath) : base(name, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (FromHost(assemblyName) is { } shared)
            return shared;

        return _resolver.ResolveAssemblyToPath(assemblyName) is { } path ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        return _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }

    private static Assembly? FromHost(AssemblyName assemblyName)
    {
        try
        {
            return s_host.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
    }
}