using HostFxrSharp;
using HostFxrSharp.Contexts;
using HostFxrSharp.Loading;

namespace Palforge.Proxy;

internal static unsafe class ProxyHost
{
    private const string CoreSubdirectory = "Palforge\\Core";
    private const string RuntimeAssembly = "Palforge.dll";
    private const string RuntimeConfig = "Palforge.runtimeconfig.json";
    private const string EntryType = "Palforge.DllMain, Palforge";
    private const string EntryMethod = "Main";

    public static void Start(string moduleDirectory)
    {
        try
        {
            Run(moduleDirectory);
        }
        catch (Exception ex)
        {
            Environment.FailFast("CLR bootstrap failed", ex);
        }
    }

    private static void Run(string moduleDirectory)
    {
        var coreDirectory = Path.Combine(moduleDirectory, CoreSubdirectory);
        var assemblyPath = Path.Combine(coreDirectory, RuntimeAssembly);
        var runtimeConfigPath = Path.Combine(coreDirectory, RuntimeConfig);

        if (!File.Exists(assemblyPath))
        {
            Environment.FailFast("Managed runtime not found", new FileNotFoundException(assemblyPath));
            return;
        }

        var bundledHostFxr = Path.Combine(coreDirectory, "hostfxr.dll");
        var selfContained = File.Exists(bundledHostFxr);

        var hostFxrPath = selfContained
            ? bundledHostFxr
            : ProxyPath.GetHostFxrDll();

        if (hostFxrPath is null)
        {
            Environment.FailFast("hostfxr.dll not found — install the .NET runtime or ship a self-contained build");
            return;
        }

        HostFxr.LoadFrom(hostFxrPath);

        using var errorWriter = HostFxr.SetErrorWriter(static message => Environment.FailFast(message));

        HostContext context;

        if (selfContained)
            context = HostFxr.InitializeForCommandLine([assemblyPath], new HostContextOptions { HostPath = assemblyPath, DotNetRoot = coreDirectory });
        else
        {
            if (!File.Exists(runtimeConfigPath))
            {
                Environment.FailFast("Runtime config not found", new FileNotFoundException(runtimeConfigPath));
                return;
            }

            context = HostFxr.InitializeForRuntimeConfig(runtimeConfigPath);
        }

        using (context)
        {
            var main = context
                .GetAssemblyFunctionPointerLoader()
                .Load(assemblyPath, EntryType, EntryMethod, MethodSignature.UnmanagedCallersOnly);

            try
            {
                ((delegate* unmanaged<void>)main)();
            }
            catch (Exception ex)
            {
                Environment.FailFast("Managed runtime failed", ex);
            }
        }
    }
}