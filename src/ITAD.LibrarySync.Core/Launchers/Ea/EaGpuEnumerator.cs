using System.Management;
using System.Runtime.Versioning;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
internal static class EaGpuEnumerator
{
    internal static IReadOnlyList<string> GetVideoControllerDeviceIds()
    {
        var ids = new List<string>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT PNPDeviceId FROM Win32_VideoController");
        foreach (var obj in searcher.Get().OfType<ManagementObject>())
        {
            var id = obj["PNPDeviceId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        return ids
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(IsDiscreteGpu)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDiscreteGpu(string deviceId) =>
        deviceId.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) ||
        deviceId.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase) ||
        deviceId.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
        deviceId.Contains("AMD", StringComparison.OrdinalIgnoreCase);
}
