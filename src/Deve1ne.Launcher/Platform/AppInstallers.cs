using System.Diagnostics;
using Deve1ne.Launcher.Core;

namespace Deve1ne.Launcher.Platform;

/// <summary>Installs, updates, removes and starts one app of the suite.</summary>
public interface IAppInstaller
{
    string InstallFolder { get; }

    /// <summary>Installed version, or null when the app is not installed.</summary>
    string? InstalledVersion();

    bool CanLaunch { get; }

    bool CanUninstall { get; }

    /// <summary>Installs the downloaded file. True when the launcher must restart to finish.</summary>
    Task<bool> InstallAsync(string downloadedFile);

    Task UninstallAsync();

    void Launch();
}

public static class AppInstallers
{
    public static IAppInstaller For(SuiteApp app) => app.Kind switch
    {
        InstallKind.Msi => new MsiAppInstaller(app),
        InstallKind.Self => new SelfAppInstaller(),
        _ => throw new ArgumentOutOfRangeException(nameof(app)),
    };
}

/// <summary>A per-user MSI installed to %LocalAppData%\Programs\&lt;folder&gt;.</summary>
public sealed class MsiAppInstaller : IAppInstaller
{
    private readonly SuiteApp _app;

    public MsiAppInstaller(SuiteApp app)
    {
        _app = app;
        InstallFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", app.InstallFolderName!);
    }

    public string InstallFolder { get; }

    private string Executable => Path.Combine(InstallFolder, _app.ExecutableName!);

    public bool CanLaunch => InstalledVersion() is not null && File.Exists(Executable);

    public bool CanUninstall => InstalledVersion() is not null;

    public string? InstalledVersion() => MsiProducts.InstalledVersion(_app.UpgradeCode!) is { } version ? VersionText.Normalize(version) : null;

    public async Task<bool> InstallAsync(string downloadedFile)
    {
        await CloseRunningAsync();
        await MsiProducts.InstallAsync(downloadedFile, LogPath("install"));
        return false;
    }

    public async Task UninstallAsync()
    {
        var productCode = MsiProducts.FindProductCode(_app.UpgradeCode!) ?? throw new InvalidOperationException($"{_app.DisplayName} is not installed.");
        await CloseRunningAsync();
        await MsiProducts.UninstallAsync(productCode, LogPath("uninstall"));
    }

    public void Launch() =>
        Process.Start(new ProcessStartInfo(Executable) { UseShellExecute = true, WorkingDirectory = InstallFolder });

    private string LogPath(string action) =>
        Path.Combine(LauncherInfo.DataFolder, "logs", $"{_app.Id}-{action}-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    /// <summary>Asks the running copies to close (they save their session), then waits for them.</summary>
    private async Task CloseRunningAsync()
    {
        var running = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_app.ExecutableName!))
            .Where(p => IsInInstallFolder(p))
            .ToList();
        foreach (var process in running)
        {
            process.CloseMainWindow();
        }

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (running.Any(p => !p.HasExited) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(250);
        }

        if (running.Any(p => !p.HasExited))
        {
            throw new InvalidOperationException($"{_app.DisplayName} is still open. Close it, then try again.");
        }
    }

    private bool IsInInstallFolder(Process process)
    {
        try
        {
            return process.MainModule?.FileName is { } path
                && path.StartsWith(InstallFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>The launcher itself: the new executable takes the place of the running one, then restarts.</summary>
public sealed class SelfAppInstaller : IAppInstaller
{
    public string InstallFolder => LauncherInfo.InstallFolder;

    public bool CanLaunch => false;

    public bool CanUninstall => LauncherInfo.RunsFromInstallFolder;

    public string? InstalledVersion() => LauncherInfo.Version;

    public Task<bool> InstallAsync(string downloadedFile)
    {
        SelfInstaller.ReplaceInstalledExecutable(downloadedFile);
        return Task.FromResult(true);
    }

    public Task UninstallAsync()
    {
        SelfInstaller.Uninstall();
        return Task.CompletedTask;
    }

    public void Launch()
    {
    }
}
