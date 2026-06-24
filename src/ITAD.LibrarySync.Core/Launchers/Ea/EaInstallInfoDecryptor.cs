using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameCollector.StoreHandlers.EADesktop.Crypto;
using NexusMods.Paths;
using SHA3.Net;

namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaInstallInfoDecryptor
{
    private const string AllUsersGenericId = "allUsersGenericId";

    private static readonly byte[] PrecomputedIv =
    [
        0x84, 0xef, 0xc4, 0xb8, 0x36, 0x11, 0x9c, 0x20, 0x41, 0x93, 0x98, 0xc3, 0xf3,
        0xf2, 0xbc, 0xef
    ];

    internal static bool TryDecrypt(
        IFileSystem fileSystem,
        AbsolutePath installInfoFile,
        IHardwareInfoProvider hardwareInfoProvider,
        out string plaintext)
    {
        plaintext = string.Empty;

        if (!fileSystem.FileExists(installInfoFile))
            return false;

        try
        {
            using var stream = fileSystem.ReadFile(installInfoFile);
            var cipherText = new byte[stream.Length];
            _ = stream.Read(cipherText);

            var key = CreateDecryptionKey(hardwareInfoProvider, System.IO.Path.GetFileName(installInfoFile.GetFullPath()));
            plaintext = DecryptFile(cipherText, key, PrecomputedIv);
            return !string.IsNullOrWhiteSpace(plaintext);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] CreateDecryptionKey(IHardwareInfoProvider hardwareInfoProvider, string category)
    {
        var hardwareString = GenerateHardwareString(hardwareInfoProvider);
        var hardwareHash = CalculateSha1Hash(hardwareString);
        var hashInput = AllUsersGenericId + category + hardwareHash;
        return CalculateSha3_256Hash(hashInput);
    }

    private static string GenerateHardwareString(IHardwareInfoProvider hardwareInfoProvider)
    {
        var builder = new StringBuilder();
        Append(builder, hardwareInfoProvider.GetBaseBoardManufacturer());
        Append(builder, hardwareInfoProvider.GetBaseBoardSerialNumber());
        Append(builder, hardwareInfoProvider.GetBIOSManufacturer());
        Append(builder, hardwareInfoProvider.GetBIOSSerialNumber());
        Append(builder, hardwareInfoProvider.GetVolumeSerialNumber());
        Append(builder, hardwareInfoProvider.GetVideoControllerDeviceId());
        Append(builder, hardwareInfoProvider.GetProcessorManufacturer());
        Append(builder, hardwareInfoProvider.GetProcessorId());
        Append(builder, hardwareInfoProvider.GetProcessorName());
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value);
        builder.Append(';');
    }

    private static string CalculateSha1Hash(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
    }

    private static byte[] CalculateSha3_256Hash(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        using var algorithm = Sha3.Sha3256();
        return algorithm.ComputeHash(bytes);
    }

    private static string DecryptFile(byte[] fileContents, byte[] key, byte[] iv)
    {
        using var cipherTextStream = new MemoryStream(fileContents, 64, fileContents.Length - 64, writable: false);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(key, iv);
        using var cryptoStream = new CryptoStream(cipherTextStream, decryptor, CryptoStreamMode.Read);
        using var decryptionStream = new StreamReader(cryptoStream);
        return decryptionStream.ReadToEnd();
    }
}
