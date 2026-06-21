using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Auth;

[SupportedOSPlatform("windows")]
public sealed class ProfileTokenStorage
{
    private readonly string _path;

    public ProfileTokenStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "profiles.dat");
    }

    public bool TryGet(LauncherId launcher, out string profileToken)
    {
        var profiles = LoadAll();
        return profiles.TryGetValue(launcher, out profileToken!);
    }

    public void Save(LauncherId launcher, string profileToken)
    {
        var profiles = LoadAll();
        profiles[launcher] = profileToken;
        SaveAll(profiles);
    }

    public void Remove(LauncherId launcher)
    {
        var profiles = LoadAll();
        if (!profiles.Remove(launcher))
            return;

        if (profiles.Count == 0)
        {
            Clear();
            return;
        }

        SaveAll(profiles);
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private Dictionary<LauncherId, string> LoadAll()
    {
        if (!File.Exists(_path))
            return [];

        var protectedBytes = File.ReadAllBytes(_path);
        var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<Dictionary<LauncherId, string>>(Encoding.UTF8.GetString(plain)) ?? [];
    }

    private void SaveAll(Dictionary<LauncherId, string> profiles)
    {
        var json = JsonSerializer.Serialize(profiles);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }
}
