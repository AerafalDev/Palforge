using System.Runtime.InteropServices;

namespace Palforge.Proxy;

internal static unsafe class ProxyForwarder
{
    private static readonly string[] s_names =
    [
        "GetFileVersionInfoA",        // 0
        "GetFileVersionInfoByHandle", // 1
        "GetFileVersionInfoExA",      // 2
        "GetFileVersionInfoExW",      // 3
        "GetFileVersionInfoSizeA",    // 4
        "GetFileVersionInfoSizeExA",  // 5
        "GetFileVersionInfoSizeExW",  // 6
        "GetFileVersionInfoSizeW",    // 7
        "GetFileVersionInfoW",        // 8
        "VerFindFileA",               // 9
        "VerFindFileW",               // 10
        "VerInstallFileA",            // 11
        "VerInstallFileW",            // 12
        "VerQueryValueA",             // 13
        "VerQueryValueW"              // 14
    ];

    private static readonly nint[] s_exports = new nint[s_names.Length];

    public static void Load()
    {
        var versionSystemPath = Path.Combine(Environment.SystemDirectory, "version.dll");

        var versionHandle = NativeLibrary.Load(versionSystemPath);

        foreach (var (index, exportName) in s_names.Index())
            s_exports[index] = NativeLibrary.GetExport(versionHandle, exportName);
    }

    private static nint GetProcAddressAtIndex(int index)
    {
        ProxyBootstrap.EnsureStarted();

        return s_exports[index];
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoA")]
    private static nint GetFileVersionInfoA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(0))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoByHandle")]
    private static nint GetFileVersionInfoByHandle(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(1))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoExA")]
    private static nint GetFileVersionInfoExA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(2))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoExW")]
    private static nint GetFileVersionInfoExW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(3))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoSizeA")]
    private static nint GetFileVersionInfoSizeA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(4))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoSizeExA")]
    private static nint GetFileVersionInfoSizeExA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(5))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoSizeExW")]
    private static nint GetFileVersionInfoSizeExW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(6))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoSizeW")]
    private static nint GetFileVersionInfoSizeW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(7))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "GetFileVersionInfoW")]
    private static nint GetFileVersionInfoW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(8))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerFindFileA")]
    private static nint VerFindFileA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(9))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerFindFileW")]
    private static nint VerFindFileW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(10))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerInstallFileA")]
    private static nint VerInstallFileA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(11))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerInstallFileW")]
    private static nint VerInstallFileW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(12))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerQueryValueA")]
    private static nint VerQueryValueA(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(13))(a1, a2, a3, a4, a5, a6, a7, a8);
    }

    [UnmanagedCallersOnly(EntryPoint = "VerQueryValueW")]
    private static nint VerQueryValueW(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6, nint a7, nint a8)
    {
        return ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint, nint, nint>)GetProcAddressAtIndex(14))(a1, a2, a3, a4, a5, a6, a7, a8);
    }
}