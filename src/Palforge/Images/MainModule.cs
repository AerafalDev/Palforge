using System.Diagnostics;

namespace Palforge.Images;

internal static class MainModule
{
    public static nint BaseAddress
    {
        get
        {
            using var process = Process.GetCurrentProcess();

            return process.MainModule?.BaseAddress ?? throw new InvalidOperationException("The process has no main module.");
        }
    }

    public static bool TryGetRange(out nint baseAddress, out long size)
    {
        using var process = Process.GetCurrentProcess();

        var module = process.MainModule;

        if (module is null)
        {
            baseAddress = 0;
            size = 0;
            return false;
        }

        baseAddress = module.BaseAddress;
        size = module.ModuleMemorySize;

        return true;
    }
}