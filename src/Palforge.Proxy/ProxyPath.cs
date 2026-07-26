namespace Palforge.Proxy;

internal static class ProxyPath
{
    public static string GetBinariesWin64Directory()
    {
        return Path.GetDirectoryName(Environment.ProcessPath) is { Length: > 0 } win64
            ? win64
            : Environment.CurrentDirectory;
    }

    public static string? GetHostFxrDll()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");

        if (string.IsNullOrEmpty(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");

        var fxrRoot = Path.Combine(root, "host", "fxr");

        if (!Directory.Exists(fxrRoot))
            return null;

        var newest = Directory.GetDirectories(fxrRoot)
            .Select(static directory => (directory, version: Version.TryParse(Path.GetFileName(directory), out var v) ? v : null))
            .Where(static candidate => candidate.version is not null)
            .OrderByDescending(static candidate => candidate.version)
            .Select(static candidate => candidate.directory)
            .FirstOrDefault();

        if (newest is null)
            return null;

        var hostFxr = Path.Combine(newest, "hostfxr.dll");

        return File.Exists(hostFxr)
            ? hostFxr
            : null;
    }
}