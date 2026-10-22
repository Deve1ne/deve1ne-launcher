using Deve1ne.Launcher.Core;

namespace Deve1ne.Launcher.Tests;

public class VersionTextTests
{
    [Theory]
    [InlineData("v0.3.0", "0.3.0")]
    [InlineData("0.3.0+03aace5139", "0.3.0")]
    [InlineData("0.3.0.0", "0.3.0")]
    [InlineData(" V1.2.3 ", "1.2.3")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    public void Normalize_strips_the_prefix_the_build_metadata_and_a_zero_revision(string input, string expected) =>
        Assert.Equal(expected, VersionText.Normalize(input));

    [Theory]
    [InlineData("0.2.1", "0.3.0", -1)]
    [InlineData("0.10.0", "0.9.9", 1)]
    [InlineData("1.0", "1.0.0", 0)]
    [InlineData("v0.3.0", "0.3.0.0", 0)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    public void Compare_orders_versions_numerically(string a, string b, int expected) =>
        Assert.Equal(expected, Math.Sign(VersionText.Compare(a, b)));
}

public class AppStatusTests
{
    [Theory]
    [InlineData(null, "0.3.0", AppState.NotInstalled)]
    [InlineData("0.2.1", "0.3.0", AppState.UpdateAvailable)]
    [InlineData("0.3.0", "0.3.0", AppState.UpToDate)]
    [InlineData("0.4.0", "0.3.0", AppState.UpToDate)]
    [InlineData("0.3.0", null, AppState.InstalledOffline)]
    [InlineData(null, null, AppState.Unavailable)]
    public void Compute_compares_the_installed_and_latest_versions(string? installed, string? latest, AppState expected) =>
        Assert.Equal(expected, AppStatus.Compute(installed, latest));
}

public class ReleaseParserTests
{
    private const string Json = """
        {
          "tag_name": "v0.3.0",
          "html_url": "https://github.com/Deve1ne/git-ui/releases/tag/v0.3.0",
          "published_at": "2026-10-20T12:30:00Z",
          "assets": [
            { "name": "GitUi-0.3.0-win-x64.msi", "browser_download_url": "https://example.test/a.msi", "size": 64000000 },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/sums", "size": 600 }
          ]
        }
        """;

    [Fact]
    public void Parse_reads_the_version_date_page_and_assets()
    {
        var release = ReleaseParser.Parse(Json);

        Assert.Equal("0.3.0", release.Version);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero), release.PublishedAt);
        Assert.Equal("https://github.com/Deve1ne/git-ui/releases/tag/v0.3.0", release.PageUrl);
        Assert.Equal(2, release.Assets.Count);
        Assert.Equal(64000000, release.Asset("gitui-0.3.0-WIN-x64.msi")!.Size);
    }

    [Fact]
    public void Catalog_asset_names_match_what_the_release_workflow_publishes()
    {
        var release = ReleaseParser.Parse(Json);

        Assert.NotNull(release.Asset(SuiteCatalog.GitUi.AssetName(release.Version)));
        Assert.Equal("Deve1ne-Launcher-0.3.0-win-x64.exe", SuiteCatalog.Launcher.AssetName("0.3.0"));
        Assert.Null(release.Asset(SuiteCatalog.Launcher.AssetName(release.Version)));
        Assert.Equal("ConvComp-0.1.0-win-x64.msi", SuiteCatalog.ConvComp.AssetName("0.1.0"));
    }

    [Fact]
    public void Catalog_apps_never_share_an_id_a_number_an_upgrade_code_or_a_folder()
    {
        var apps = SuiteCatalog.All.Append(SuiteCatalog.Launcher).ToList();

        Assert.Equal(apps.Count, apps.Select(a => a.Id).Distinct().Count());
        Assert.Equal(apps.Count, apps.Select(a => a.Number).Distinct().Count());
        var msiApps = apps.Where(a => a.Kind == InstallKind.Msi).ToList();
        Assert.Equal(msiApps.Count, msiApps.Select(a => a.UpgradeCode!.ToUpperInvariant()).Distinct().Count());
        Assert.Equal(msiApps.Count, msiApps.Select(a => a.InstallFolderName!.ToUpperInvariant()).Distinct().Count());
    }
}

public class ChecksumsTests
{
    [Fact]
    public void Parse_reads_sha256sum_output_in_text_and_binary_modes()
    {
        var a = new string('a', 64);
        var b = new string('B', 64);
        var sums = Checksums.Parse($"{a}  GitUi-0.3.0-win-x64.msi\n{b} *gitui_0.3.0_amd64.deb\r\nnot a line\n");

        Assert.Equal(2, sums.Count);
        Assert.Equal(a, sums["GitUi-0.3.0-win-x64.msi"]);
        Assert.Equal(new string('b', 64), sums["gitui_0.3.0_amd64.deb"]);
    }

    [Fact]
    public void Parse_ignores_lines_whose_hash_is_not_hexadecimal()
    {
        var sums = Checksums.Parse(new string('z', 64) + "  file.txt");

        Assert.Empty(sums);
    }
}
