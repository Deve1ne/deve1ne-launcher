using Avalonia;
using Deve1ne.Launcher.Platform;

// The launcher installs MSI packages and writes the user's registry: Windows only.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace Deve1ne.Launcher;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Settings > Apps > Uninstall runs "Deve1neLauncher.exe --uninstall".
        if (args.Contains(SelfInstaller.UninstallArgument))
        {
            SelfInstaller.Uninstall();
            return;
        }

        // Started from the download: install to the user's programs folder and continue from there.
        if (!args.Contains("--portable") && SelfInstaller.InstallIfNeeded())
        {
            return;
        }

        SelfInstaller.CleanUp();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
