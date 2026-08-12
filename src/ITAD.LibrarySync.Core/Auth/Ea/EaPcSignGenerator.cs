using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITAD.LibrarySync.Core.Launchers.Ea;

namespace ITAD.LibrarySync.Core.Auth.Ea;

[SupportedOSPlatform("windows")]
public static class EaPcSignGenerator
{
    private static readonly byte[] SignKeyV1 = "ISa3dpGOc8wW7Adn4auACSQmaccrOyR2"u8.ToArray();
    private static readonly byte[] SignKeyV2 = "nt5FfJbdPzNcl2pkC3zgjO43Knvscxft"u8.ToArray();

    public static string Generate()
    {
        var hardware = EaPcSignHardware.Collect();
        var signVersion = Random.Shared.Next(2) == 0 ? "v1" : "v2";
        var signKey = signVersion == "v1" ? SignKeyV1 : SignKeyV2;
        var timestamp = DateTimeOffset.UtcNow;

        var payload = new Dictionary<string, object?>
        {
            ["av"] = "v1",
            ["bsn"] = hardware.BiosSerial,
            ["gid"] = hardware.GpuDeviceId,
            ["hsn"] = hardware.DiskSerial,
            ["mac"] = hardware.Mac,
            ["mid"] = hardware.MachineId,
            ["msn"] = hardware.BoardSerial,
            ["sv"] = signVersion,
            ["ts"] = $"{timestamp:yyyy-M-d H:m:s}:{timestamp.Millisecond}"
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signature = HMACSHA256.HashData(signKey, Encoding.ASCII.GetBytes(encodedPayload));
        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class EaPcSignHardware
    {
        public required string BoardSerial { get; init; }
        public required string BiosSerial { get; init; }
        public required string DiskSerial { get; init; }
        public required string Mac { get; init; }
        public required string MachineId { get; init; }
        public required int GpuDeviceId { get; init; }

        public static EaPcSignHardware Collect()
        {
            var boardManufacturer = QueryWmi("Win32_BaseBoard", "Manufacturer") ?? "Microsoft Corporation";
            var boardSerial = QueryWmi("Win32_BaseBoard", "SerialNumber") ?? "None";
            var biosManufacturer = QueryWmi("Win32_BIOS", "Manufacturer") ?? "Microsoft Corporation";
            var biosSerial = QueryWmi("Win32_BIOS", "SerialNumber") ?? "None";
            var osInstallDate = QueryWmi("Win32_OperatingSystem", "InstallDate") ?? "19700101000000.000000+000";
            var osSerial = QueryWmi("Win32_OperatingSystem", "SerialNumber") ?? "None";
            var diskSerial = QueryWmi("Win32_DiskDrive WHERE Index=0", "SerialNumber") ?? "None";
            var volumeSerial = GetVolumeSerial();
            var gpuPnpId = EaGpuEnumerator.GetVideoControllerDeviceIds().FirstOrDefault() ?? "None";
            var gpuDeviceId = ParseGpuDeviceId(gpuPnpId);
            var mac = GetEaMacAddress();

            var machineBuffer = new StringBuilder()
                .Append(boardManufacturer)
                .Append(boardSerial)
                .Append(biosManufacturer)
                .Append(biosSerial)
                .Append(osInstallDate)
                .Append(osSerial);
            if (mac is not null)
                machineBuffer.Append(mac);

            return new EaPcSignHardware
            {
                BoardSerial = boardSerial,
                BiosSerial = biosSerial,
                DiskSerial = diskSerial,
                Mac = mac ?? string.Empty,
                MachineId = HashFnv1a(machineBuffer.ToString()).ToString(),
                GpuDeviceId = gpuDeviceId
            };
        }

        private static ulong HashFnv1a(string value)
        {
            const ulong offset = 0xcbf29ce484222325;
            const ulong prime = 0x100000001b3;
            var hash = offset;
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                hash ^= b;
                hash = unchecked(hash * prime);
            }

            return hash;
        }

        private static int ParseGpuDeviceId(string pnpId)
        {
            const string marker = "DEV_";
            var index = pnpId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return 0;

            var hex = pnpId[(index + marker.Length)..];
            var end = hex.IndexOf('&');
            if (end > 0)
                hex = hex[..end];

            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var deviceId)
                ? deviceId
                : 0;
        }

        private static string? GetEaMacAddress()
        {
            var node = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().GetAddressBytes())
                .FirstOrDefault(bytes => bytes.Length == 6 && (bytes[0] & 0x01) == 0);

            if (node is null)
                return null;

            return "$" + Convert.ToHexString(node).ToLowerInvariant();
        }

        private static string GetVolumeSerial()
        {
            if (!OperatingSystem.IsWindows())
                return "00000000";

            var serial = 0u;
            if (GetVolumeInformationW("C:\\", null, 0, ref serial, out _, out _, null, 0))
                return serial.ToString("x8");

            return "00000000";
        }

        private static string? QueryWmi(string className, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var value = obj[property]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch
            {
                // WMI unavailable in restricted environments.
            }

            return null;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool GetVolumeInformationW(
            string rootPath,
            StringBuilder? volumeName,
            int volumeNameCapacity,
            ref uint serialNumber,
            out uint maxComponentLength,
            out uint fileSystemFlags,
            StringBuilder? fileSystemName,
            int fileSystemNameCapacity);
    }
}
