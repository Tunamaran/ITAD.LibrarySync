using GameCollector.StoreHandlers.EADesktop.Crypto;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal sealed class GpuOverrideHardwareInfoProvider(
    IHardwareInfoProvider inner,
    string videoControllerDeviceId) : IHardwareInfoProvider
{
    public string GetVolumeSerialNumber() => inner.GetVolumeSerialNumber();

    public string GetBaseBoardManufacturer() => inner.GetBaseBoardManufacturer();

    public string GetBaseBoardSerialNumber() => inner.GetBaseBoardSerialNumber();

    public string GetBIOSManufacturer() => inner.GetBIOSManufacturer();

    public string GetBIOSSerialNumber() => inner.GetBIOSSerialNumber();

    public string GetVideoControllerDeviceId() => videoControllerDeviceId;

    public string GetProcessorManufacturer() => inner.GetProcessorManufacturer();

    public string GetProcessorId() => inner.GetProcessorId();

    public string GetProcessorName() => inner.GetProcessorName();
}
