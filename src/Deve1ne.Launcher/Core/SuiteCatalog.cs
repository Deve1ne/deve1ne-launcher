namespace Deve1ne.Launcher.Core;

/// <summary>How an app of the suite gets onto the machine.</summary>
public enum InstallKind
{
    /// <summary>A per-user MSI, found again through its upgrade code.</summary>
    Msi,

    /// <summary>The launcher itself: a single executable that replaces itself.</summary>
    Self,
}

/// <summary>One app of the suite, as the launcher shows it.</summary>
/// <param name="Id">Stable identifier, also the name of its icon: Assets/apps/{Id}.png.</param>
/// <param name="Number">Position in the suite ("01"), shown in the colored tag.</param>
/// <param name="Name">The plain part of the name ("Git ").</param>
/// <param name="Accent">The colored part of the name ("UI").</param>
/// <param name="AccentColor">Tag and name accent, as #RRGGBB.</param>
/// <param name="Repository">GitHub "owner/name" whose latest release is installed.</param>
/// <param name="AssetPattern">Release asset name with {version}, e.g. "GitUi-{version}-win-x64.msi".</param>
/// <param name="UpgradeCode">MSI upgrade code (Msi only).</param>
/// <param name="InstallFolderName">Folder below %LocalAppData%\Programs (Msi only).</param>
/// <param name="ExecutableName">Main executable in the install folder (Msi only).</param>
public sealed record SuiteApp(
    string Id,
    string Number,
    string Name,
    string Accent,
    string Tagline,
    string AccentColor,
    InstallKind Kind,
    string Repository,
    string AssetPattern,
    string? WebsiteUrl = null,
    string? UpgradeCode = null,
    string? InstallFolderName = null,
    string? ExecutableName = null)
{
    public string DisplayName => (Name + Accent).Trim();

    public string GitHubUrl => "https://github.com/" + Repository;

    public string HomeUrl => WebsiteUrl ?? GitHubUrl + "#readme";

    public string IconUri => $"avares://Deve1neLauncher/Assets/apps/{Id}.png";

    /// <summary>The release asset of this app for the given version, e.g. "GitUi-0.3.0-win-x64.msi".</summary>
    public string AssetName(string version) => AssetPattern.Replace("{version}", version);
}

/// <summary>
/// Every app the launcher knows. Adding an app: an entry here, its icon in Assets/apps/{id}.png, and
/// releases in its repository with a Windows asset named after <see cref="SuiteApp.AssetPattern"/>
/// plus a SHA256SUMS.txt listing it.
/// </summary>
public static class SuiteCatalog
{
    public const string LauncherRepository = "Deve1ne/deve1ne-launcher";
    public const string ProfileUrl = "https://github.com/Deve1ne";

    public static readonly SuiteApp GitUi = new(
        Id: "git-ui",
        Number: "01",
        Name: "Git ",
        Accent: "UI",
        Tagline: "Desktop Git client, graph and diffs",
        AccentColor: "#1592B0",
        Kind: InstallKind.Msi,
        Repository: "Deve1ne/git-ui",
        AssetPattern: "GitUi-{version}-win-x64.msi",
        UpgradeCode: "{A5BBCD08-2555-4A84-95FB-3B959C296B16}",
        InstallFolderName: "GitUi",
        ExecutableName: "GitUi.Presentation.exe");

    public static readonly SuiteApp ConvComp = new(
        Id: "convcomp",
        Number: "02",
        Name: "Conv",
        Accent: "Comp",
        Tagline: "Convert and compress media",
        AccentColor: "#9B6FE0",
        Kind: InstallKind.Msi,
        Repository: "Deve1ne/convcomp",
        AssetPattern: "ConvComp-{version}-win-x64.msi",
        UpgradeCode: "{D5DCAEDD-EF6B-44E5-84CE-5B05CB1CFEC4}",
        InstallFolderName: "ConvComp",
        ExecutableName: "ConvComp.exe");

    /// <summary>The launcher itself: not listed with the apps, it updates from the title bar.</summary>
    public static readonly SuiteApp Launcher = new(
        Id: "launcher",
        Number: "00",
        Name: "Deve1ne ",
        Accent: "Launcher",
        Tagline: "Installs and updates the suite",
        AccentColor: "#E8792B",
        Kind: InstallKind.Self,
        Repository: LauncherRepository,
        AssetPattern: "Deve1ne-Launcher-{version}-win-x64.exe");

    /// <summary>The apps shown on the page, in suite order.</summary>
    public static IReadOnlyList<SuiteApp> All { get; } = new[] { GitUi, ConvComp };
}
