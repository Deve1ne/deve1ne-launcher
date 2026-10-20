using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Deve1ne.Launcher.Core;
using Deve1ne.Launcher.Platform;

namespace Deve1ne.Launcher.ViewModels;

public enum AppFilter
{
    All,
    Installed,
    Updates,
    NotInstalled,
}

/// <summary>The launcher window: the apps of the suite, filters, sort, search and the GitHub check.</summary>
public partial class LauncherViewModel : ObservableObject
{
    public static readonly string[] SortOptions = { "Suite order", "Name", "Status" };

    private readonly GitHubClient _gitHub = new();
    private DateTimeOffset? _checkedAt;

    public LauncherViewModel()
    {
        Settings = LauncherSettings.Load();
        isTableView = Settings.TableView;
        foreach (var app in SuiteCatalog.All)
        {
            Apps.Add(new AppCardViewModel(app, AppInstallers.For(app), _gitHub, this));
        }

        ApplyFilter();
    }

    public LauncherSettings Settings { get; }

    public ObservableCollection<AppCardViewModel> Apps { get; } = new();

    public ObservableCollection<AppCardViewModel> VisibleApps { get; } = new();

    /// <summary>Set by the window: asks a yes/no question (title, message, confirm label).</summary>
    public Func<string, string, string, Task<bool>>? Confirm { get; set; }

    /// <summary>Set by the window: closes the app.</summary>
    public Action? RequestExit { get; set; }

    public bool HasChecked => _checkedAt is not null || CheckError is not null;

    [ObservableProperty] private AppFilter filter = AppFilter.All;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string sort = SortOptions[0];
    [ObservableProperty] private bool isTableView;
    [ObservableProperty] private bool isChecking;
    [ObservableProperty] private string? checkError;
    [ObservableProperty] private string statusText = "CHECKING…";
    [ObservableProperty] private int allCount;
    [ObservableProperty] private int installedCount;
    [ObservableProperty] private int updatesCount;
    [ObservableProperty] private int notInstalledCount;
    [ObservableProperty] private int columns = 3;

    public string VersionText => $"LAUNCHER {LauncherInfo.Version}";

    public bool IsGridView
    {
        get => !IsTableView;
        set
        {
            if (value)
            {
                IsTableView = false;
            }
        }
    }

    // One bool per filter segment (radio buttons bind two-way to them).
    public bool FilterAll { get => Filter == AppFilter.All; set => SelectFilter(value, AppFilter.All); }

    public bool FilterInstalled { get => Filter == AppFilter.Installed; set => SelectFilter(value, AppFilter.Installed); }

    public bool FilterUpdates { get => Filter == AppFilter.Updates; set => SelectFilter(value, AppFilter.Updates); }

    public bool FilterNotInstalled { get => Filter == AppFilter.NotInstalled; set => SelectFilter(value, AppFilter.NotInstalled); }

    private void SelectFilter(bool selected, AppFilter value)
    {
        if (selected)
        {
            Filter = value;
        }
    }

    public bool LaunchAfterInstall
    {
        get => Settings.LaunchAfterInstall;
        set
        {
            Settings.LaunchAfterInstall = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public bool KeepDownloads
    {
        get => Settings.KeepDownloads;
        set
        {
            Settings.KeepDownloads = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    partial void OnFilterChanged(AppFilter value)
    {
        OnPropertyChanged(nameof(FilterAll));
        OnPropertyChanged(nameof(FilterInstalled));
        OnPropertyChanged(nameof(FilterUpdates));
        OnPropertyChanged(nameof(FilterNotInstalled));
        ApplyFilter();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnSortChanged(string value) => ApplyFilter();

    partial void OnIsTableViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGridView));
        Settings.TableView = value;
        Settings.Save();
    }

    [RelayCommand]
    private void SetFilter(AppFilter value) => Filter = value;

    [RelayCommand]
    private void ShowGrid() => IsTableView = false;

    [RelayCommand]
    private void ShowTable() => IsTableView = true;

    [RelayCommand]
    private void OpenGitHub() => OpenUrl(SuiteCatalog.ProfileUrl);

    [RelayCommand]
    private void OpenWebsite() => OpenUrl("https://github.com/" + SuiteCatalog.LauncherRepository + "#readme");

    [RelayCommand]
    private void OpenReleases() => OpenUrl("https://github.com/" + SuiteCatalog.LauncherRepository + "/releases");

    [RelayCommand]
    private void OpenDownloads()
    {
        Directory.CreateDirectory(LauncherInfo.DownloadFolder);
        Process.Start(new ProcessStartInfo(LauncherInfo.DownloadFolder) { UseShellExecute = true });
    }

    [RelayCommand]
    public async Task CheckAsync()
    {
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        StatusText = "CHECKING…";

        // One request per repository, all at once; a failing repository only leaves its apps offline.
        var repositories = Apps.Select(a => a.App.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lookups = repositories.ToDictionary(r => r, r => _gitHub.GetLatestReleaseAsync(r), StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (var (repository, lookup) in lookups)
        {
            try
            {
                await lookup;
            }
            catch (Exception ex)
            {
                errors.Add(ex is HttpRequestException or TaskCanceledException ? "Could not reach GitHub." : ex.Message);
            }
        }

        IsChecking = false;
        CheckError = errors.Count == repositories.Count ? errors[0] : null;
        if (errors.Count < repositories.Count)
        {
            _checkedAt = DateTimeOffset.Now;
        }

        foreach (var app in Apps)
        {
            var lookup = lookups[app.App.Repository];
            app.Refresh(lookup.IsCompletedSuccessfully ? lookup.Result : null, lookup.IsCompletedSuccessfully);
        }

        UpdateCounts();
        UpdateStatusText();
    }

    /// <summary>Called every 30 s by the window: "checked 3 min ago".</summary>
    public void UpdateStatusText()
    {
        if (IsChecking)
        {
            return;
        }

        if (CheckError is not null)
        {
            StatusText = CheckError.ToUpperInvariant() + " · CLICK ↻ TO RETRY";
            return;
        }

        if (_checkedAt is { } at)
        {
            var minutes = (int)(DateTimeOffset.Now - at).TotalMinutes;
            var when = minutes switch
            {
                < 1 => "JUST NOW",
                < 60 => $"{minutes} MIN AGO",
                _ => $"{minutes / 60} H AGO",
            };
            StatusText = $"CHECKED {when} · VIA GITHUB RELEASES";
        }
    }

    public void UpdateCounts()
    {
        AllCount = Apps.Count;
        InstalledCount = Apps.Count(a => a.InstalledVersion is not null);
        UpdatesCount = Apps.Count(a => a.State == AppState.UpdateAvailable);
        NotInstalledCount = Apps.Count(a => a.InstalledVersion is null);
        ApplyFilter();
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        Confirm?.Invoke(title, message, confirmLabel) ?? Task.FromResult(false);

    public void Exit() => RequestExit?.Invoke();

    /// <summary>The launcher replaced its own executable: start the new one and quit.</summary>
    public void RestartIntoUpdate()
    {
        try
        {
            SelfInstaller.RestartInstalled();
        }
        finally
        {
            Exit();
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser registered: nothing useful to do.
        }
    }

    private void ApplyFilter()
    {
        var text = Search.Trim();
        var matching = Apps.Where(a => Filter switch
            {
                AppFilter.Installed => a.InstalledVersion is not null,
                AppFilter.Updates => a.State == AppState.UpdateAvailable,
                AppFilter.NotInstalled => a.InstalledVersion is null,
                _ => true,
            })
            .Where(a => text.Length == 0
                || a.App.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || a.App.Tagline.Contains(text, StringComparison.OrdinalIgnoreCase));

        matching = Sort switch
        {
            "Name" => matching.OrderBy(a => a.App.DisplayName, StringComparer.OrdinalIgnoreCase),
            "Status" => matching.OrderBy(a => a.State switch
            {
                AppState.UpdateAvailable => 0,
                AppState.NotInstalled => 1,
                _ => 2,
            }).ThenBy(a => a.App.Number),
            _ => matching.OrderBy(a => a.App.Number),
        };

        var list = matching.ToList();
        if (list.SequenceEqual(VisibleApps))
        {
            return;
        }

        VisibleApps.Clear();
        foreach (var app in list)
        {
            VisibleApps.Add(app);
        }
    }
}
