using GameCollector.StoreHandlers.EADesktop.Crypto;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal sealed class EaPlaceholderHardwareInfoProvider(IHardwareInfoProvider inner) : IHardwareInfoProvider
{
    public string GetVolumeSerialNumber() => inner.GetVolumeSerialNumber();

    public string GetBaseBoardManufacturer() => Normalize(inner.GetBaseBoardManufacturer());

    public string GetBaseBoardSerialNumber() => Normalize(inner.GetBaseBoardSerialNumber());

    public string GetBIOSManufacturer() => Normalize(inner.GetBIOSManufacturer());

    public string GetBIOSSerialNumber() => Normalize(inner.GetBIOSSerialNumber());

    public string GetVideoControllerDeviceId() => inner.GetVideoControllerDeviceId();

    public string GetProcessorManufacturer() => Normalize(inner.GetProcessorManufacturer());

    public string GetProcessorId() => inner.GetProcessorId();

    public string GetProcessorName() => Normalize(inner.GetProcessorName());

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim() switch
        {
            "To be filled by O.E.M." => string.Empty,
            "Default string" => string.Empty,
            "None" => string.Empty,
            "Not Specified" => string.Empty,
            "System Serial Number" => string.Empty,
            "System Product Name" => string.Empty,
            _ => value.Trim()
        };
    }
}
