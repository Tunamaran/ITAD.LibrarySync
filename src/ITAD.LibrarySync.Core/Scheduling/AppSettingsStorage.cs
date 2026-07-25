using System.Text.Json;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.Core.Scheduling;

public enum AppLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public SyncInterval Interval { get; set; } = SyncInterval.Disabled;
    public bool SyncOnStartup { get; set; }
    public bool HasCompletedFirstRun { get; set; }
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool ConfirmBeforeSync { get; set; } = true;
    public AppLogLevel LogLevel { get; set; } = AppLogLevel.Info;
    public Dictionary<LauncherId, bool> EnabledLaunchers { get; set; } = CreateDefaultEnabledLaunchers();
    public Dictionary<LauncherId, string> LastSyncStatsByLauncher { get; set; } = new();
    public string? ItadUsername { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public string? LastSyncSummary { get; set; }

    public SyncScheduleOptions ToSyncScheduleOptions() => new()
    {
        Interval = Interval,
        SyncOnStartup = SyncOnStartup
    };

    public void ApplySyncScheduleOptions(SyncScheduleOptions options)
    {
        Interval = options.Interval;
        SyncOnStartup = options.SyncOnStartup;
    }

    public bool IsLauncherEnabled(LauncherId launcher) =>
        !EnabledLaunchers.TryGetValue(launcher, out var enabled) || enabled;

    public IReadOnlyList<LauncherId> GetEnabledLaunchers() =>
        Enum.GetValues<LauncherId>().Where(IsLauncherEnabled).ToList();

    private static Dictionary<LauncherId, bool> CreateDefaultEnabledLaunchers() =>
        Enum.GetValues<LauncherId>().ToDictionary(id => id, _ => true);
}

public sealed class AppSettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public AppSettingsStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITADLibrarySync");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }

    public SyncScheduleOptions LoadSyncScheduleOptions() => Load().ToSyncScheduleOptions();

    public void SaveSyncScheduleOptions(SyncScheduleOptions options)
    {
        var settings = Load();
        settings.ApplySyncScheduleOptions(options);
        Save(settings);
    }
}
