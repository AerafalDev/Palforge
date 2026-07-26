using System.Runtime.InteropServices;
using Palforge.Hosting;

namespace Palforge;

internal static class DllMain
{
    [UnmanagedCallersOnly]
    public static void Main()
    {
        try
        {
            PalforgeApplication.Start();
        }
        catch (Exception exception)
        {
            Environment.FailFast("Palforge runtime failed to start", exception);
        }
    }
}