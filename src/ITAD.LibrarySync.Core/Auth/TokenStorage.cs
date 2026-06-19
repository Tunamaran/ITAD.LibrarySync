using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITAD.LibrarySync.Core.Auth;

[SupportedOSPlatform("windows")]
public sealed class TokenStorage
{
    private readonly string _path;

    public TokenStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "tokens.dat");
    }

    public void Save(OAuthTokens tokens)
    {
        var json = JsonSerializer.Serialize(tokens);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public OAuthTokens? Load()
    {
        if (!File.Exists(_path))
            return null;

        var protectedBytes = File.ReadAllBytes(_path);
        var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<OAuthTokens>(Encoding.UTF8.GetString(plain));
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
