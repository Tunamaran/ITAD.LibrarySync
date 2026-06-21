using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ITAD.LibrarySync.App.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ITAD Library Sync";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open Windows Run registry key.");

        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not determine application executable path.");

            key.SetValue(ValueName, $"\"{exePath}\"");
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
