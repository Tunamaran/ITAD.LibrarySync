using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITAD.LibrarySync.Core.Auth.Xbox;

[SupportedOSPlatform("windows")]
public sealed class XboxTokenStorage
{
    private readonly string _loginPath;
    private readonly string _xstsPath;
    private readonly string _licensingXstsPath;

    public XboxTokenStorage(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _loginPath = Path.Combine(dir, "xbox-login.dat");
        _xstsPath = Path.Combine(dir, "xbox-xsts.dat");
        _licensingXstsPath = Path.Combine(dir, "xbox-licensing-xsts.dat");
    }

    public void SaveLogin(XboxOAuthTokens tokens)
    {
        WriteProtected(_loginPath, JsonSerializer.Serialize(tokens));
    }

    public XboxOAuthTokens? LoadLogin()
    {
        return ReadProtected<XboxOAuthTokens>(_loginPath);
    }

    public void ClearLogin()
    {
        DeleteIfExists(_loginPath);
    }

    public void SaveXsts(XboxAuthorizationData data)
    {
        WriteProtected(_xstsPath, JsonSerializer.Serialize(data));
    }

    public XboxAuthorizationData? LoadXsts()
    {
        return ReadProtected<XboxAuthorizationData>(_xstsPath);
    }

    public void ClearXsts()
    {
        DeleteIfExists(_xstsPath);
    }

    public void SaveLicensingXsts(XboxAuthorizationData data)
    {
        WriteProtected(_licensingXstsPath, JsonSerializer.Serialize(data));
    }

    public XboxAuthorizationData? LoadLicensingXsts()
    {
        return ReadProtected<XboxAuthorizationData>(_licensingXstsPath);
    }

    public void ClearLicensingXsts()
    {
        DeleteIfExists(_licensingXstsPath);
    }

    public void ClearAll()
    {
        ClearLogin();
        ClearXsts();
        ClearLicensingXsts();
    }

    private static void WriteProtected(string path, string json)
    {
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    private static T? ReadProtected<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        var protectedBytes = File.ReadAllBytes(path);
        var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(plain));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
