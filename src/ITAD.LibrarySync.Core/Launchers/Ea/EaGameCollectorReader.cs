using System.Runtime.Versioning;
using GameCollector.StoreHandlers.EADesktop;
using GameCollector.StoreHandlers.EADesktop.Crypto;
using GameCollector.StoreHandlers.EADesktop.Crypto.Windows;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using NexusMods.Paths;
using OneOf;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

[SupportedOSPlatform("windows")]
internal static class EaGameCollectorReader
{
    internal static IEnumerable<OneOf<EADesktopGame, ErrorMessage>> FindAllGames(Settings settings)
    {
        var hardwareInfoProvider = new HardwareInfoProvider();
        var gpuCandidates = BuildGpuCandidates(hardwareInfoProvider);

        IReadOnlyList<OneOf<EADesktopGame, ErrorMessage>>? lastResults = null;
        foreach (var gpuId in gpuCandidates)
        {
            var provider = CreateProvider(hardwareInfoProvider, gpuId);
            var handler = new EADesktopHandler(FileSystem.Shared, WindowsRegistry.Shared, provider);
            var results = handler.FindAllGames(settings).ToList();
            lastResults = results;

            if (!EaDecryptFailureDetector.IsDecryptFailure(results))
                return results;
        }

        return lastResults ?? [];
    }

    private static IReadOnlyList<string?> BuildGpuCandidates(IHardwareInfoProvider hardwareInfoProvider)
    {
        var candidates = new List<string?>();
        foreach (var gpuId in EaGpuEnumerator.GetVideoControllerDeviceIds())
            candidates.Add(gpuId);

        try
        {
            candidates.Add(hardwareInfoProvider.GetVideoControllerDeviceId());
        }
        catch
        {
            if (candidates.Count == 0)
                candidates.Add(null);
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IHardwareInfoProvider CreateProvider(
        IHardwareInfoProvider hardwareInfoProvider,
        string? gpuId)
    {
        if (gpuId is null)
            return hardwareInfoProvider;

        try
        {
            if (string.Equals(
                    gpuId,
                    hardwareInfoProvider.GetVideoControllerDeviceId(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return hardwareInfoProvider;
            }
        }
        catch
        {
            // Fall back to the explicit GPU override below.
        }

        return new GpuOverrideHardwareInfoProvider(hardwareInfoProvider, gpuId);
    }
}
