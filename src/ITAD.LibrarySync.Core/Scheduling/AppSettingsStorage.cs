using System.Text.Json;

namespace ITAD.LibrarySync.Core.Scheduling;

public sealed class AppSettings
{
    public SyncInterval Interval { get; set; } = SyncInterval.Disabled;
    public bool SyncOnStartup { get; set; }
    public bool HasCompletedFirstRun { get; set; }

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
