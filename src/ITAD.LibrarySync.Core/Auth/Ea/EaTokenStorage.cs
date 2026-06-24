using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ITAD.LibrarySync.Core.Auth.Ea;

[SupportedOSPlatform("windows")]
public sealed class EaTokenStorage
{
    private readonly string _loginPath;
    private readonly string _sessionPath;

    public EaTokenStorage(string? baseDirectory = null)
    {
        var dir = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _loginPath = Path.Combine(dir, "ea-login.dat");
        _sessionPath = Path.Combine(dir, "ea-session.dat");
    }

    public void SaveLogin(EaOAuthTokens tokens) =>
        WriteProtected(_loginPath, JsonSerializer.Serialize(tokens));

    public EaOAuthTokens? LoadLogin() =>
        ReadProtected<EaOAuthTokens>(_loginPath);

    public void ClearLogin() =>
        DeleteIfExists(_loginPath);

    public void SaveSession(EaSessionInfo session) =>
        WriteProtected(_sessionPath, JsonSerializer.Serialize(session));

    public EaSessionInfo? LoadSession() =>
        ReadProtected<EaSessionInfo>(_sessionPath);

    public void ClearSession() =>
        DeleteIfExists(_sessionPath);

    public void ClearAll()
    {
        ClearLogin();
        ClearSession();
    }

    private static void WriteProtected(string path, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    private static T? ReadProtected<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes));
        }
        catch
        {
            return default;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
