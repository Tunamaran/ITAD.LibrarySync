using GameCollector.StoreHandlers.EADesktop.Crypto;
using GameCollector.StoreHandlers.EADesktop.Crypto.Windows;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaHardwareCandidateFactory
{
    internal static IEnumerable<IHardwareInfoProvider> CreateCandidates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var baseProvider = new HardwareInfoProvider();
        var seeds = new List<IHardwareInfoProvider> { baseProvider, new EaPlaceholderHardwareInfoProvider(baseProvider) };

        foreach (var gpuId in EaGpuEnumerator.GetVideoControllerDeviceIds())
        {
            seeds.Add(new GpuOverrideHardwareInfoProvider(baseProvider, gpuId));
            seeds.Add(new GpuOverrideHardwareInfoProvider(
                new EaPlaceholderHardwareInfoProvider(baseProvider),
                gpuId));
        }

        foreach (var provider in seeds)
        {
            var key = Describe(provider);
            if (seen.Add(key))
                yield return provider;
        }
    }

    private static string Describe(IHardwareInfoProvider provider)
    {
        try
        {
            return string.Join('|',
                provider.GetVolumeSerialNumber(),
                provider.GetBaseBoardSerialNumber(),
                provider.GetBIOSSerialNumber(),
                provider.GetVideoControllerDeviceId(),
                provider.GetProcessorId());
        }
        catch
        {
            return provider.GetType().Name + Guid.NewGuid().ToString("N");
        }
    }
}
