namespace MonitorService.Configuration;

/// <summary>
/// Resuelve la "DataDirectory" efectiva y rutas relativas a ella.
/// Reglas: vacío => junto al .exe; relativa => respecto al .exe; absoluta => tal cual.
/// </summary>
public static class PathResolver
{
    public static string ResolveDataDirectory(string? configured, string exeDir)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return exeDir;
        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(exeDir, configured));
    }

    public static string ResolveUnder(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(root, path));
    }
}
