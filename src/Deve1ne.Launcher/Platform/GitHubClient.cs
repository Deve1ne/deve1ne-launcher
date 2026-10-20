using System.Net.Http.Headers;
using System.Security.Cryptography;
using Deve1ne.Launcher.Core;

namespace Deve1ne.Launcher.Platform;

/// <summary>Reads the latest releases from the public GitHub API and downloads their files.</summary>
public sealed class GitHubClient
{
    private static readonly HttpClient Http = CreateClient();

    /// <summary>The latest release of "owner/name", or null when the repository has none yet.</summary>
    public async Task<Release?> GetLatestReleaseAsync(string repository, CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if ((int)response.StatusCode == 403 && response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0")
        {
            throw new InvalidOperationException("GitHub API rate limit reached. Try again in a few minutes.");
        }

        response.EnsureSuccessStatusCode();
        return ReleaseParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<string> GetTextAsync(string url, CancellationToken cancellationToken = default) =>
        await Http.GetStringAsync(url, cancellationToken);

    /// <summary>Downloads to <paramref name="path"/>, reporting 0..1, and returns the file's lowercase SHA-256.</summary>
    public async Task<string> DownloadAsync(string url, string path, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            done += read;
            if (total > 0)
            {
                progress.Report((double)done / total);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Deve1ne-Launcher", LauncherInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
