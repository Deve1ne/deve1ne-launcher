using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deve1ne.Launcher.Core;
using Deve1ne.Launcher.Platform;

namespace Deve1ne.Launcher.ViewModels;

/// <summary>One card (or table row): an app, its state against the latest release, and its actions.</summary>
public partial class AppCardViewModel : ObservableObject
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private readonly IAppInstaller _installer;
    private readonly GitHubClient _gitHub;
    private readonly LauncherViewModel _owner;
    private Release? _release;
    private bool _reachable = true;

    public AppCardViewModel(SuiteApp app, IAppInstaller installer, GitHubClient gitHub, LauncherViewModel owner)
    {
        App = app;
        _installer = installer;
        _gitHub = gitHub;
        _owner = owner;
        AccentBrush = new SolidColorBrush(Color.Parse(app.AccentColor));
        Icon = new Bitmap(AssetLoader.Open(new Uri(app.IconUri)));
        Refresh(null);
    }

    public SuiteApp App { get; }

    public IBrush AccentBrush { get; }

    public Bitmap Icon { get; }

    public bool IsMsi => App.Kind == InstallKind.Msi;

    public bool IsLauncher => App.Kind == InstallKind.Self;

    [ObservableProperty] private AppState state = AppState.Unknown;
    [ObservableProperty] private string? installedVersion;
    [ObservableProperty] private string badgeText = "CHECKING…";
    [ObservableProperty] private IBrush badgeForeground = Brushes.Gray;
    [ObservableProperty] private IBrush badgeBackground = Brushes.Transparent;
    [ObservableProperty] private IBrush badgeBorder = Brushes.Gray;
    [ObservableProperty] private string versionLine = "Looking for the latest release…";
    [ObservableProperty] private string? installedLine;
    [ObservableProperty] private string primaryText = "…";
    [ObservableProperty] private bool primaryEnabled;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool hasProgress;
    [ObservableProperty] private string? errorText;
    [ObservableProperty] private bool isVisible = true;

    public bool CanLaunch => _installer.CanLaunch;

    public bool CanUninstall => !IsBusy && _installer.CanUninstall;

    public bool CanReinstall => !IsBusy && InstalledVersion is not null && LatestAsset() is not null && !IsLauncher;

    public bool HasRelease => _release is not null;

    public DateTimeOffset PublishedAt => _release?.PublishedAt ?? DateTimeOffset.MinValue;

    /// <summary>
    /// Re-reads the installed version and compares it with <paramref name="release"/>: null when the
    /// repository has no release yet (<paramref name="reachable"/>) or GitHub could not be reached.
    /// </summary>
    public void Refresh(Release? release, bool reachable = true)
    {
        _release = release;
        _reachable = reachable;
        InstalledVersion = _installer.InstalledVersion();
        var latest = LatestAsset() is not null ? release!.Version : null;
        State = release is null && InstalledVersion is null && !_owner.HasChecked ? AppState.Unknown : AppStatus.Compute(InstalledVersion, latest);

        VersionLine = release is null
            ? (!_owner.HasChecked ? "Looking for the latest release…"
                : reachable ? "No release published yet"
                : "Latest version unknown (offline)")
            : latest is null
                ? $"No Windows build in {release.Version}"
                : $"Version {release.Version} · {release.PublishedAt.ToLocalTime().ToString("d MMM yyyy", English)}";
        InstalledLine = State == AppState.UpdateAvailable || State == AppState.InstalledOffline
            ? $"Installed: {InstalledVersion}"
            : null;

        if (!IsBusy)
        {
            ApplyState();
        }

        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanReinstall));
        OnPropertyChanged(nameof(HasRelease));
    }

    private ReleaseAsset? LatestAsset() => _release?.Asset(App.AssetName(_release.Version));

    private void ApplyState()
    {
        var (text, color) = State switch
        {
            AppState.NotInstalled => ("AVAILABLE TO INSTALL", "#2B5FD9"),
            AppState.UpdateAvailable => ($"UPDATE AVAILABLE · {InstalledVersion} → {_release!.Version}", "#C2560E"),
            AppState.UpToDate => (IsLauncher ? "UP TO DATE · RUNNING" : "INSTALLED · UP TO DATE", "#1C7C3C"),
            AppState.InstalledOffline => ("INSTALLED", "#1C7C3C"),
            AppState.Unavailable => ("NOT AVAILABLE", "#7A776F"),
            _ => ("CHECKING…", "#7A776F"),
        };
        SetBadge(text, color);

        (PrimaryText, PrimaryEnabled) = State switch
        {
            AppState.NotInstalled => ("INSTALL", true),
            AppState.UpdateAvailable => ("UPDATE", true),
            AppState.UpToDate or AppState.InstalledOffline when CanLaunch => ("OPEN", true),
            AppState.UpToDate or AppState.InstalledOffline => ("UP TO DATE", false),
            _ => ("INSTALL", false),
        };
    }

    private void SetBadge(string text, string hex)
    {
        var color = Color.Parse(hex);
        BadgeText = text;
        BadgeForeground = new SolidColorBrush(color);
        BadgeBackground = new SolidColorBrush(color, 0.07);
        BadgeBorder = new SolidColorBrush(color, 0.35);
    }

    [RelayCommand]
    private async Task Primary()
    {
        switch (State)
        {
            case AppState.NotInstalled or AppState.UpdateAvailable:
                await InstallAsync();
                break;
            case AppState.UpToDate or AppState.InstalledOffline when CanLaunch:
                Launch();
                break;
        }
    }

    [RelayCommand]
    private Task Reinstall() => InstallAsync();

    [RelayCommand]
    private void Launch() => Guard(() => _installer.Launch());

    [RelayCommand]
    private void OpenFolder() => Guard(() =>
    {
        if (Directory.Exists(_installer.InstallFolder))
        {
            Process.Start(new ProcessStartInfo(_installer.InstallFolder) { UseShellExecute = true });
        }
    });

    [RelayCommand]
    private void ReleaseNotes() => LauncherViewModel.OpenUrl(_release?.PageUrl ?? App.GitHubUrl + "/releases");

    [RelayCommand]
    private void OpenWebsite() => LauncherViewModel.OpenUrl(App.HomeUrl);

    [RelayCommand]
    private void OpenGitHub() => LauncherViewModel.OpenUrl(App.GitHubUrl);

    [RelayCommand]
    private async Task Uninstall()
    {
        var question = IsLauncher
            ? "Remove the launcher from this computer? The apps it installed stay installed."
            : $"Remove {App.DisplayName} from this computer? Your settings and repositories are kept.";
        if (!await _owner.ConfirmAsync($"Uninstall {App.DisplayName}", question, "UNINSTALL"))
        {
            return;
        }

        await RunAsync("UNINSTALLING…", async () =>
        {
            await _installer.UninstallAsync();
            if (IsLauncher)
            {
                _owner.Exit();
            }
        });
    }

    private async Task InstallAsync()
    {
        if (_release is not { } release || LatestAsset() is not { } asset)
        {
            return;
        }

        var restart = false;
        await RunAsync("DOWNLOADING", async () =>
        {
            var sums = release.Asset("SHA256SUMS.txt")
                ?? throw new InvalidOperationException("The release publishes no checksums; the download cannot be verified.");
            var expected = Checksums.Parse(await _gitHub.GetTextAsync(sums.DownloadUrl)).GetValueOrDefault(asset.Name)
                ?? throw new InvalidOperationException($"No checksum for {asset.Name} in the release.");

            var file = Path.Combine(LauncherInfo.DownloadFolder, asset.Name);
            HasProgress = true;
            var hash = await _gitHub.DownloadAsync(asset.DownloadUrl, file, new Progress<double>(value =>
            {
                Progress = value * 100;
                SetBadge($"DOWNLOADING · {value:P0}", "#141414");
            }));
            HasProgress = false;
            if (hash != expected)
            {
                File.Delete(file);
                throw new InvalidOperationException("The download is corrupted (checksum mismatch). Try again.");
            }

            SetBadge(IsLauncher ? "UPDATING THE LAUNCHER…" : "INSTALLING…", "#141414");
            PrimaryText = "INSTALLING…";
            restart = await _installer.InstallAsync(file);
            if (!_owner.Settings.KeepDownloads)
            {
                TryDelete(file);
            }
        });

        if (restart)
        {
            _owner.RestartIntoUpdate();
        }
        else if (ErrorText is null && _owner.Settings.LaunchAfterInstall && CanLaunch)
        {
            Launch();
        }
    }

    private async Task RunAsync(string busyText, Func<Task> action)
    {
        IsBusy = true;
        ErrorText = null;
        PrimaryEnabled = false;
        PrimaryText = busyText;
        SetBadge(busyText, "#141414");
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanReinstall));
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            HasProgress = false;
            Progress = 0;
            Refresh(_release, _reachable);
            _owner.UpdateCounts();
        }
    }

    private void Guard(Action action)
    {
        try
        {
            ErrorText = null;
            action();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
