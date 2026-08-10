using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ITAD.LibrarySync.Core.Services;

/// <summary>
/// Creates and removes NTFS directory junctions. Unlike symbolic links,
/// junctions do NOT require administrator privileges, so the main app can
/// create them without a UAC elevation prompt.
/// </summary>
internal static class JunctionHelper
{
    internal static bool IsJunction(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a directory junction at <paramref name="linkPath"/> pointing to
    /// <paramref name="targetPath"/>. Returns <c>null</c> on success, otherwise an error message.
    /// </summary>
    internal static string? TryCreate(string linkPath, string targetPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // Escape '%' as '%%' so cmd does not expand environment variables
                // inside the quoted paths (NTFS allows '%' in folder names).
                Arguments = $"/c mklink /J \"{EscapeForCmd(linkPath)}\" \"{EscapeForCmd(targetPath)}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return "Failed to start mklink.";

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15_000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignore
                }

                return "mklink timed out.";
            }

            if (process.ExitCode != 0)
                return $"mklink failed (exit {process.ExitCode}): {Combine(output, error)}";

            if (!IsJunction(linkPath))
                return $"Junction was not created: {Combine(output, error)}";

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Removes a junction without touching its target folder.
    /// Returns <c>null</c> on success, otherwise an error message.
    /// </summary>
    internal static string? TryDelete(string junctionPath)
    {
        // RemoveDirectory on a reparse point removes only the link itself.
        if (RemoveDirectory(junctionPath))
            return null;

        var lastError = Marshal.GetLastWin32Error();

        // Fallback for edge cases where the P/Invoke is not permitted.
        try
        {
            if (IsJunction(junctionPath) && Directory.Exists(junctionPath))
            {
                Directory.Delete(junctionPath, recursive: false);
                return null;
            }
        }
        catch (Exception ex)
        {
            return $"Failed to remove junction: {ex.Message}";
        }

        return $"Failed to remove junction (Win32 error {lastError}).";
    }

    private static string EscapeForCmd(string path) => path.Replace("%", "%%");

    private static string Combine(string output, string error)
    {
        var combined = $"{output}{error}".Trim();
        return string.IsNullOrWhiteSpace(combined) ? "no output" : combined;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveDirectory(string lpPathName);
}
