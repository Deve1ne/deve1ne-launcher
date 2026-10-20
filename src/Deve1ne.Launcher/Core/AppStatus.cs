namespace Deve1ne.Launcher.Core;

public static class VersionText
{
    /// <summary>"v0.3.0" → "0.3.0"; "0.3.0+abc" → "0.3.0"; "0.3.0.0" → "0.3.0".</summary>
    public static string Normalize(string version)
    {
        var text = version.Trim().TrimStart('v', 'V').Split('+')[0];
        var parts = text.Split('.');
        return parts.Length == 4 && parts[3] == "0" ? string.Join('.', parts[..3]) : text;
    }

    /// <summary>Compares dotted versions numerically (a pre-release suffix sorts before the release).</summary>
    public static int Compare(string a, string b)
    {
        static (int[] Numbers, string Suffix) Split(string version)
        {
            var text = Normalize(version);
            var dash = text.IndexOf('-');
            var suffix = dash >= 0 ? text[(dash + 1)..] : "";
            var core = dash >= 0 ? text[..dash] : text;
            return (core.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray(), suffix);
        }

        var (left, leftSuffix) = Split(a);
        var (right, rightSuffix) = Split(b);
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var difference = (i < left.Length ? left[i] : 0).CompareTo(i < right.Length ? right[i] : 0);
            if (difference != 0)
            {
                return difference;
            }
        }

        if (leftSuffix.Length == 0 || rightSuffix.Length == 0)
        {
            return rightSuffix.Length.CompareTo(leftSuffix.Length) switch { > 0 => 1, < 0 => -1, _ => 0 };
        }

        return string.CompareOrdinal(leftSuffix, rightSuffix);
    }
}

public enum AppState
{
    /// <summary>Not checked yet.</summary>
    Unknown,
    NotInstalled,
    UpdateAvailable,
    UpToDate,

    /// <summary>Installed, but GitHub could not be reached or has no build of it.</summary>
    InstalledOffline,

    /// <summary>Not installed and nothing to install (GitHub unreachable or no asset).</summary>
    Unavailable,
}

public static class AppStatus
{
    /// <param name="installed">Installed version, null when absent.</param>
    /// <param name="latest">Latest published version that has an asset for this app, null when unknown.</param>
    public static AppState Compute(string? installed, string? latest) => (installed, latest) switch
    {
        (null, null) => AppState.Unavailable,
        (null, _) => AppState.NotInstalled,
        (_, null) => AppState.InstalledOffline,
        _ => VersionText.Compare(installed, latest) < 0 ? AppState.UpdateAvailable : AppState.UpToDate,
    };
}
