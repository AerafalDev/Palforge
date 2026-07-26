namespace Palforge.Extensions;

public static class PathExtensions
{
    private static class PalforgePath
    {
        public static string Win64Directory { get; }

        public static string RootDirectory { get; }

        public static string CoreDirectory { get; }

        public static string LogsDirectory { get; }

        public static string PluginsDirectory { get; }

        public static string CacheDirectory { get; }

        public static string MappingsDirectory { get; }

        public static string DumpsDirectory { get; }

        static PalforgePath()
        {
            Win64Directory = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            RootDirectory = Path.Combine(Win64Directory, "Palforge");
            CoreDirectory = Path.Combine(RootDirectory, "Core");
            LogsDirectory = Path.Combine(RootDirectory, "Logs");
            PluginsDirectory = Path.Combine(RootDirectory, "Plugins");
            CacheDirectory = Path.Combine(RootDirectory, "Cache");
            MappingsDirectory = Path.Combine(RootDirectory, "Mappings");
            DumpsDirectory = Path.Combine(RootDirectory, "Dumps");
        }
    }

    extension(Path)
    {
        public static string PalforgeWin64Directory =>
            PalforgePath.Win64Directory;

        public static string PalforgeRootDirectory =>
            PalforgePath.RootDirectory;

        public static string PalforgeCoreDirectory =>
            PalforgePath.CoreDirectory;

        public static string PalforgeLogsDirectory =>
            PalforgePath.LogsDirectory;

        public static string PalforgePluginsDirectory =>
            PalforgePath.PluginsDirectory;

        public static string PalforgeCacheDirectory =>
            PalforgePath.CacheDirectory;

        public static string PalforgeMappingsDirectory =>
            PalforgePath.MappingsDirectory;

        public static string PalforgeDumpsDirectory =>
            PalforgePath.DumpsDirectory;
    }
}