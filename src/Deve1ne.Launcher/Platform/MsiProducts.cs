using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Deve1ne.Launcher.Platform;

/// <summary>Windows Installer queries and msiexec runs for the per-user MSI apps.</summary>
public static class MsiProducts
{
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private const int ErrorSuccessRebootRequired = 3010;
    private const int ErrorInstallUserExit = 1602;

    /// <summary>Product code of the installed product with this upgrade code, or null.</summary>
    public static string? FindProductCode(string upgradeCode)
    {
        var productCode = new StringBuilder(39);
        return MsiEnumRelatedProducts(upgradeCode, 0, 0, productCode) == ErrorSuccess ? productCode.ToString() : null;
    }

    /// <summary>Installed version ("0.2.1") of the product with this upgrade code, or null.</summary>
    public static string? InstalledVersion(string upgradeCode) =>
        FindProductCode(upgradeCode) is { } productCode ? ProductInfo(productCode, "VersionString") : null;

    public static Task InstallAsync(string msiPath, string logPath) =>
        RunAsync($"/i \"{msiPath}\" /qn /norestart /l*v \"{logPath}\"", logPath);

    public static Task UninstallAsync(string productCode, string logPath) =>
        RunAsync($"/x {productCode} /qn /norestart /l*v \"{logPath}\"", logPath);

    private static async Task RunAsync(string arguments, string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        using var process = Process.Start(new ProcessStartInfo("msiexec.exe", arguments) { UseShellExecute = false, CreateNoWindow = true })
            ?? throw new InvalidOperationException("Could not start Windows Installer.");
        await process.WaitForExitAsync();
        switch (process.ExitCode)
        {
            case ErrorSuccess or ErrorSuccessRebootRequired:
                return;
            case ErrorInstallUserExit:
                throw new OperationCanceledException("The installation was cancelled.");
            default:
                throw new InvalidOperationException($"Windows Installer failed (code {process.ExitCode}). Log: {logPath}");
        }
    }

    private static string? ProductInfo(string productCode, string property)
    {
        var length = 64;
        var value = new StringBuilder(length);
        var result = MsiGetProductInfo(productCode, property, value, ref length);
        if (result == ErrorMoreData)
        {
            value = new StringBuilder(++length);
            result = MsiGetProductInfo(productCode, property, value, ref length);
        }

        return result == ErrorSuccess && value.Length > 0 ? value.ToString() : null;
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern int MsiEnumRelatedProducts(string upgradeCode, int reserved, int index, StringBuilder productCode);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern int MsiGetProductInfo(string product, string property, StringBuilder value, ref int length);
}
