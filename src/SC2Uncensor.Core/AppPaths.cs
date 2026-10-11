namespace SC2Uncensor.Core;

public static class AppPaths
{
    public static string Root { get; } = ResolveRoot();
    public static string Offsets => Path.Combine(Root, "offsets.dat");
    public static string Config => Path.Combine(Root, "config.json");
    public static string LogFile => Path.Combine(Root, "sc2_uncensor.log");

    private static string ResolveRoot()
    {
        var env = Environment.GetEnvironmentVariable("SC2_UNCENSOR_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
