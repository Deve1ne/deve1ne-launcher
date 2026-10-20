using System.Diagnostics;
using Microsoft.Win32;

namespace Deve1ne.Launcher.Platform;

/// <summary>
/// The launcher ships as one executable. Started from anywhere else (the Downloads folder), it copies
/// itself to %LocalAppData%\Programs\Deve1ne Launcher, adds a Start menu shortcut and an entry in
/// Settings &gt; Apps, then restarts from there. No administrator rights are needed.
/// </summary>
public static class SelfInstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Deve1neLauncher";
    public const string UninstallArgument = "--uninstall";

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Deve1ne Launcher.lnk");

    /// <summary>
    /// Installs the running executable when it does not run from the install folder.
    /// True when an installed copy was started and this process should exit.
    /// </summary>
    public static bool InstallIfNeeded()
    {
        if (!OperatingSystem.IsWindows() || LauncherInfo.RunsFromInstallFolder)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(LauncherInfo.InstallFolder);
            var installed = LauncherInfo.InstalledExecutable;
            if (!File.Exists(installed) || IsNewer(LauncherInfo.CurrentExecutable, installed))
            {
                if (File.Exists(installed))
                {
                    MoveAside(installed);
                }

                File.Copy(LauncherInfo.CurrentExecutable, installed, overwrite: true);
            }

            Register();
            Process.Start(new ProcessStartInfo(installed) { UseShellExecute = true, WorkingDirectory = LauncherInfo.InstallFolder });
            return true;
        }
        catch (Exception)
        {
            // Read-only profile, antivirus lock...: run from where it is instead.
            return false;
        }
    }

    /// <summary>Puts a downloaded launcher in place of the installed one (the running file can be renamed, not overwritten).</summary>
    public static void ReplaceInstalledExecutable(string downloadedFile)
    {
        var installed = LauncherInfo.InstalledExecutable;
        Directory.CreateDirectory(LauncherInfo.InstallFolder);
        if (File.Exists(installed))
        {
            MoveAside(installed);
        }

        File.Copy(downloadedFile, installed, overwrite: true);
        Register();
    }

    /// <summary>Starts the installed launcher; the caller then exits.</summary>
    public static void RestartInstalled() =>
        Process.Start(new ProcessStartInfo(LauncherInfo.InstalledExecutable) { UseShellExecute = true, WorkingDirectory = LauncherInfo.InstallFolder });

    /// <summary>Deletes the copies moved aside by earlier updates.</summary>
    public static void CleanUp()
    {
        try
        {
            foreach (var old in Directory.EnumerateFiles(LauncherInfo.InstallFolder, "*.old"))
            {
                try
                {
                    File.Delete(old);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    /// <summary>Removes the shortcut, the Apps entry and (once this process has exited) the install folder.</summary>
    public static void Uninstall()
    {
        File.Delete(ShortcutPath);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        var folder = LauncherInfo.InstallFolder;
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{folder}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static void Register()
    {
        var installed = LauncherInfo.InstalledExecutable;
        CreateShortcut(ShortcutPath, installed);

        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "Deve1ne Launcher");
        key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(installed).ProductVersion?.Split('+')[0] ?? LauncherInfo.Version);
        key.SetValue("Publisher", "Dimitri Even");
        key.SetValue("DisplayIcon", installed);
        key.SetValue("InstallLocation", LauncherInfo.InstallFolder);
        key.SetValue("UninstallString", $"\"{installed}\" {UninstallArgument}");
        key.SetValue("URLInfoAbout", "https://github.com/Deve1ne/deve1ne-launcher");
        key.SetValue("EstimatedSize", (int)(new FileInfo(installed).Length / 1024), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(string path, string target)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = Path.GetDirectoryName(target);
        shortcut.IconLocation = target + ",0";
        shortcut.Description = "Install and update the Deve1ne apps";
        shortcut.Save();
    }

    private static void MoveAside(string file)
    {
        var aside = file + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".old";
        File.Move(file, aside);
    }

    private static bool IsNewer(string candidate, string installed)
    {
        static string Version(string file) => FileVersionInfo.GetVersionInfo(file).ProductVersion ?? "0";
        return Core.VersionText.Compare(Version(candidate), Version(installed)) > 0;
    }
}
