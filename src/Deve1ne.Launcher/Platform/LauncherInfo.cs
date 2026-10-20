using System.Reflection;

namespace Deve1ne.Launcher.Platform;

public static class LauncherInfo
{
    /// <summary>The launcher's own version, e.g. "0.3.0" ("0.0.0" when not stamped).</summary>
    public static string Version { get; } =
        typeof(LauncherInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    /// <summary>Where the launcher installs itself: %LocalAppData%\Programs\Deve1ne Launcher.</summary>
    public static string InstallFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Deve1ne Launcher");

    public static string InstalledExecutable => Path.Combine(InstallFolder, "Deve1neLauncher.exe");

    /// <summary>Settings, downloads and logs: %LocalAppData%\Deve1ne\Launcher.</summary>
    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Deve1ne", "Launcher");

    public static string DownloadFolder => Path.Combine(DataFolder, "downloads");

    public static string CurrentExecutable => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Deve1neLauncher.exe");

    public static bool RunsFromInstallFolder =>
        string.Equals(Path.GetFullPath(CurrentExecutable), Path.GetFullPath(InstalledExecutable), StringComparison.OrdinalIgnoreCase);
}
