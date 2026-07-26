using System.Runtime.InteropServices;

namespace Palforge;

internal static class DllMain
{
    [UnmanagedCallersOnly]
    public static void Main()
    {
        try
        {
        }
        catch (Exception exception)
        {
            Environment.FailFast("Palforge runtime failed to start", exception);
        }
    }
}