using System.Text.Json;

namespace Deve1ne.Launcher.Core;

public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);

/// <summary>A GitHub release: its version (tag without the "v"), date, page and files.</summary>
public sealed record Release(string Version, DateTimeOffset PublishedAt, string PageUrl, IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? Asset(string name) =>
        Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
}

public static class ReleaseParser
{
    /// <summary>Reads the JSON of GET /repos/{owner}/{repo}/releases/latest.</summary>
    public static Release Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? throw new FormatException("The release has no tag.");
        var published = root.TryGetProperty("published_at", out var date) && date.ValueKind == JsonValueKind.String
            ? date.GetDateTimeOffset()
            : DateTimeOffset.MinValue;
        var page = root.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "";
        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in list.EnumerateArray())
            {
                assets.Add(new ReleaseAsset(
                    asset.GetProperty("name").GetString() ?? "",
                    asset.GetProperty("browser_download_url").GetString() ?? "",
                    asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0));
            }
        }

        return new Release(VersionText.Normalize(tag), published, page, assets);
    }
}

public static class Checksums
{
    /// <summary>Reads a sha256sum listing ("hash  name" or "hash *name" per line) into name → lowercase hash.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var sums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var space = line.IndexOf(' ');
            if (space != 64)
            {
                continue;
            }

            var hash = line[..64];
            if (!hash.All(Uri.IsHexDigit))
            {
                continue;
            }

            var name = line[space..].TrimStart(' ', '*');
            if (name.Length > 0)
            {
                sums[name] = hash.ToLowerInvariant();
            }
        }

        return sums;
    }
}
