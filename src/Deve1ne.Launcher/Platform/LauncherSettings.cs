using System.Text.Json;

namespace Deve1ne.Launcher.Platform;

/// <summary>The few launcher options, in %LocalAppData%\Deve1ne\Launcher\settings.json.</summary>
public sealed class LauncherSettings
{
    public bool LaunchAfterInstall { get; set; } = true;

    public bool KeepDownloads { get; set; }

    public bool TableView { get; set; }

    private static string FilePath => Path.Combine(LauncherInfo.DataFolder, "settings.json");

    public static LauncherSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(FilePath)) ?? new LauncherSettings()
                : new LauncherSettings();
        }
        catch (Exception)
        {
            return new LauncherSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(LauncherInfo.DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Settings are a convenience: a read-only profile must not break the launcher.
        }
    }
}
