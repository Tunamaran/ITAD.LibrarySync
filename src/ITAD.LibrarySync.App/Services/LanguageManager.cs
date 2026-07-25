using System.ComponentModel;
using System.Runtime.CompilerServices;
using ITAD.LibrarySync.Core.Scheduling;

namespace ITAD.LibrarySync.App.Services;

public sealed class LanguageOption(string code, string displayName)
{
    public string Code { get; } = code;
    public string DisplayName { get; } = displayName;

    public override string ToString() => DisplayName;
}

public sealed class LanguageManager : INotifyPropertyChanged
{
    public static LanguageManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _currentLanguage = "en";

    public static readonly IReadOnlyList<LanguageOption> AvailableLanguages =
    [
        new("en", "English (Default)"),
        new("tr", "Türkçe")
    ];

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(string.Empty); // Notifies all indexers and properties in WPF
            }
        }
    }

    public void Initialize(AppSettingsStorage storage)
    {
        var settings = storage.Load();
        CurrentLanguage = settings.Language is "tr" ? "tr" : "en";
    }

    public string this[string key] => GetString(key);

    public string GetString(string key) => _currentLanguage switch
    {
        "tr" => GetTurkishString(key),
        _ => GetEnglishString(key)
    };

    private static string GetEnglishString(string key) => key switch
    {
        "SettingsTitle" => "ITAD Library Sync — Settings",
        "TabItad" => "ITAD Connection",
        "TabPlatforms" => "Platforms",
        "TabSync" => "Synchronization",
        "TabStats" => "Statistics",
        "TabUnmatched" => "Unmatched Games",
        "TabLogs" => "Logs",
        "TabGeneral" => "General",

        "ItadHeader" => "IsThereAnyDeal Account",
        "ConnectionStatusLabel" => "Connection Status:",
        "StatusConnected" => "Connected",
        "StatusNotConnected" => "Not Connected",
        "UsernameLabel" => "Username:",
        "ConnectBtn" => "Connect to ITAD",
        "DisconnectBtn" => "Disconnect",
        "SyncNowBtn" => "Sync Now",
        "ItadHowItWorksTitle" => "How it works?",
        "ItadHowItWorksText" => "By connecting your ITAD account, your local game libraries will be automatically synced. Game titles are automatically corrected using the Smart Match Engine, and obsolete mismatched entries are purged from your profile.",

        "PlatformTipsTitle" => "Platform Scanning Tips:",
        "PlatformTipsText" => "• Local Platforms (Epic, Ubisoft, Battle.net, EA App): Requires client app installed & run at least once. No account credentials required.\n• Xbox / Microsoft Store: Link your Xbox account below for complete library & Game Pass sync.",
        "ColPlatform" => "Platform",
        "ColEnabled" => "Enabled",
        "ColStatus" => "Status",
        "ColLastSync" => "Last Sync",
        "ColResult" => "Result",
        "ColTest" => "Test",
        "ColDetails" => "Details",
        "BtnTest" => "Test",
        "BtnDetails" => "Details",
        "TestBtnTooltip" => "Quickly scans and tests this platform's game library on your PC or account.",
        "DetailsBtnTooltip" => "Displays detected directory path, scanning method, and full list of found games.",
        "XboxAccountHeader" => "Xbox / Microsoft Store Account",
        "BtnConnectXbox" => "Link Xbox Account",
        "BtnDisconnectXbox" => "Disconnect Xbox",

        "SyncSettingsHeader" => "Automatic Synchronization Settings",
        "SyncGuideTitle" => "Automatic Sync Guide:",
        "SyncGuideText" => "The app runs silently in the system tray to keep your libraries updated at your selected interval. Newly acquired games are detected automatically.",
        "SyncIntervalLabel" => "Sync Frequency",
        "SyncOnStartupLabel" => "Sync automatically on application startup",
        "ConfirmBeforeSyncLabel" => "Ask for confirmation before manual sync",
        "SyncIntervalTooltip" => "Determines how frequently your game libraries are automatically scanned and sent to ITAD.",
        "SyncOnStartupTooltip" => "Instantly syncs your libraries every time your PC or app starts.",
        "ConfirmBeforeSyncTooltip" => "Displays a preview confirmation window before sending games when you click 'Sync Now'.",

        "StatsHeader" => "Library Statistics & Summary",
        "TotalSyncedGamesLabel" => "Total Synced Games",
        "MatchRateLabel" => "ITAD Match Rate",
        "ActivePlatformsLabel" => "Active Platforms",
        "CustomMappingsHeader" => "Custom Game Mappings",
        "ColGameTitle" => "Game Title",
        "ColLocalStoreId" => "Local Store ID",
        "ColTargetItadId" => "Target ITAD ID",
        "BtnDelete" => "Delete",
        "DeleteMappingTooltip" => "Deletes this custom mapping rule.",

        "UnmatchedGuideTitle" => "Manual Match Fix Guide:",
        "UnmatchedGuideText" => "Games found on your local platforms whose titles do not exactly match ITAD's database appear here. Click 'Fix' to map the correct ITAD title.",
        "BtnClearList" => "Clear List",
        "BtnRefresh" => "Refresh",
        "BtnFix" => "Fix",
        "ClearListTooltip" => "Clears the recorded unmatched titles history.",
        "RefreshListTooltip" => "Reloads the unmatched titles list.",
        "ColReason" => "Reason",
        "ColDate" => "Date",
        "ColAction" => "Action",

        "LogsGuideText" => "Live system execution logs for library scanning, matching, and ITAD API sync processes.",
        "BtnOpenLogFolder" => "Open Log Folder",
        "OpenLogFolderTooltip" => "Opens the log files directory in Windows Explorer.",
        "RefreshLogsTooltip" => "Reloads live system logs.",
        "LogFilterTooltip" => "Filter by log level (ALL, INFO, ERROR)",
        "ColTime" => "Time",
        "ColLevel" => "Level",
        "ColMessage" => "Message Details",

        "AppSubtitle" => "IsThereAnyDeal Game Library Synchronization Tool",
        "AppPreferencesHeader" => "Application Preferences",
        "StartWithWindowsLabel" => "Run on Windows startup",
        "ShowNotificationsLabel" => "Show desktop notifications",
        "LanguageLabel" => "Language / Dil",
        "LogLevelLabel" => "Log Level",
        "AppUpdatesHeader" => "Application Updates",
        "BtnCheckUpdates" => "Check for Updates",
        "BtnDownloadUpdate" => "Download & Update",
        "BtnClose" => "Close",
        "VersionPrefix" => "Version",
        "VersionUpToDate" => "Your version is kept up to date.",

        "StartWithWindowsTooltip" => "Starts the app automatically in the system tray when Windows boots.",
        "ShowNotificationsTooltip" => "Displays Windows desktop notifications when sync completes or encounters an error.",
        "LanguageTooltip" => "Select the application display language (English / Türkçe).",
        "LogLevelTooltip" => "Sets log detail level (Info, Error, Debug).",

        "TraySyncNow" => "Sync Now",
        "TraySyncStore" => "Sync {0}",
        "TraySettings" => "Settings…",
        "TrayViewLog" => "View Latest Log File",
        "TrayCheckUpdates" => "Check for Updates…",
        "TrayDisconnect" => "Disconnect ITAD",
        "TrayConnect" => "Connect to ITAD",
        "TrayExit" => "Exit",
        "ExitConfirmText" => "A sync is in progress. Exit anyway?",
        "ExitConfirmTitle" => "Exit ITAD Library Sync",

        _ => key
    };

    private static string GetTurkishString(string key) => key switch
    {
        "SettingsTitle" => "ITAD Library Sync — Ayarlar",
        "TabItad" => "ITAD Bağlantısı",
        "TabPlatforms" => "Platformlar",
        "TabSync" => "Senkronizasyon",
        "TabStats" => "İstatistikler",
        "TabUnmatched" => "Eşleşmeyenler",
        "TabLogs" => "Loglar",
        "TabGeneral" => "Genel",

        "ItadHeader" => "IsThereAnyDeal Hesabı",
        "ConnectionStatusLabel" => "Bağlantı Durumu:",
        "StatusConnected" => "Bağlı",
        "StatusNotConnected" => "Bağlı Değil",
        "UsernameLabel" => "Kullanıcı Adı:",
        "ConnectBtn" => "ITAD'a Bağlan",
        "DisconnectBtn" => "Bağlantıyı Kes",
        "SyncNowBtn" => "Şimdi Senkronize Et",
        "ItadHowItWorksTitle" => "Nasıl Çalışır?",
        "ItadHowItWorksText" => "ITAD hesabınızı bağlayarak yerel kütüphanelerinizi otomatik aktarabilirsiniz. Oyun isimleri Akıllı Eşleştirme Motoru tarafından otomatik düzeltilir ve eski hatalı kayıtlar profilinizden temizlenir.",

        "PlatformTipsTitle" => "Platform Taraması İpuçları:",
        "PlatformTipsText" => "• Yerel Platformlar (Epic, Ubisoft, Battle.net, EA App): İstemci uygulamasının yüklü ve en az 1 kez çalıştırılmış olması yeterlidir. Ekstra hesap bağlama gerektirmez.\n• Xbox / Microsoft Store: Game Pass ve mağaza oyunlarının tam aktarılması için aşağıdan Xbox hesabınızı bağlamanız önerilir.",
        "ColPlatform" => "Platform",
        "ColEnabled" => "Etkin",
        "ColStatus" => "Durum",
        "ColLastSync" => "Son Senkronizasyon",
        "ColResult" => "Sonuç",
        "ColTest" => "Test",
        "ColDetails" => "Detay",
        "BtnTest" => "Test Et",
        "BtnDetails" => "Detaylar",
        "TestBtnTooltip" => "Bu platformun oyun kütüphanesini bilgisayarınızda veya hesabınızda hızlıca taranıp test edilmesini sağlar.",
        "DetailsBtnTooltip" => "Platformun tespit edildiği klasör yolunu, tarama metodunu ve tespit edilen tüm oyun listesini görüntüler.",
        "XboxAccountHeader" => "Xbox / Microsoft Store Hesabı",
        "BtnConnectXbox" => "Xbox Bağla",
        "BtnDisconnectXbox" => "Xbox Bağlantısını Kes",

        "SyncSettingsHeader" => "Otomatik Senkronizasyon Ayarları",
        "SyncGuideTitle" => "Otomatik Senkronizasyon Rehberi:",
        "SyncGuideText" => "Seçtiğiniz zaman aralığında uygulama arka planda çalışarak kütüphanelerinizi güncel tutar. Bilgisayarınızı veya oyun istemcilerinizi açıp kapattığınızda yeni alınan oyunlar otomatik algılanır.",
        "SyncIntervalLabel" => "Senkronizasyon Sıklığı",
        "SyncOnStartupLabel" => "Uygulama açılışında otomatik senkronize et",
        "ConfirmBeforeSyncLabel" => "Elle senkronizasyon öncesi onay iste",
        "SyncIntervalTooltip" => "Arka planda oyun kütüphanelerinizin ne sıklıkla otomatik taranacağını ve ITAD'a gönderileceğini belirler.",
        "SyncOnStartupTooltip" => "Bilgisayar veya uygulama her başladığında kütüphanelerinizi anında senkronize eder.",
        "ConfirmBeforeSyncTooltip" => "Şimdi Senkronize Et butonuna bastığınızda aktarılacak oyun listesini onaylamanız için önizleme penceresi açar.",

        "StatsHeader" => "Kütüphane İstatistikleri ve Özet",
        "TotalSyncedGamesLabel" => "Toplam Okunan Oyun",
        "MatchRateLabel" => "ITAD Eşleşme Oranı",
        "ActivePlatformsLabel" => "Aktif Platform",
        "CustomMappingsHeader" => "Özel Eşleşme Kuralları (Custom Mappings)",
        "ColGameTitle" => "Oyun Adı",
        "ColLocalStoreId" => "Yerel Mağaza ID",
        "ColTargetItadId" => "Hedef ITAD ID",
        "BtnDelete" => "Sil",
        "DeleteMappingTooltip" => "Bu özel eşleştirme kuralını siler.",

        "UnmatchedGuideTitle" => "Eşleşmeyen Oyunları Manuel Eşleştirme Rehberi:",
        "UnmatchedGuideText" => "Yerel platformlarınızda bulunan ancak ismi ITAD veritabanındaki resmi ismiyle birebir örtüşmeyen oyunlar burada listelenir. 'Düzelt' butonuna basarak doğru ITAD oyun linkini tanıtabilirsiniz.",
        "BtnClearList" => "Listeyi Temizle",
        "BtnRefresh" => "Yenile",
        "BtnFix" => "Düzelt",
        "ClearListTooltip" => "Eşleşmeyen oyun kayıt geçmişini temizler.",
        "RefreshListTooltip" => "Eşleşmeyen oyunlar listesini yeniden yükler.",
        "ColReason" => "Neden",
        "ColDate" => "Tarih",
        "ColAction" => "Eylem",

        "LogsGuideText" => "Uygulamanın kütüphane tarama, eşleştirme ve ITAD iletişim süreçlerine ait canlı günlüklerdir.",
        "BtnOpenLogFolder" => "Log Klasörünü Aç",
        "OpenLogFolderTooltip" => "Log dosyalarının saklandığı klasörü Windows Gezgini'nde açar.",
        "RefreshLogsTooltip" => "Canlı log akışını yeniden yükler.",
        "LogFilterTooltip" => "Log seviyesine göre filtrele (ALL, INFO, ERROR)",
        "ColTime" => "Zaman",
        "ColLevel" => "Seviye",
        "ColMessage" => "Mesaj Detayı",

        "AppSubtitle" => "IsThereAnyDeal Oyun Kütüphanesi Senkronizasyon Aracı",
        "AppPreferencesHeader" => "Uygulama Tercihleri",
        "StartWithWindowsLabel" => "Windows açılışında çalıştır",
        "ShowNotificationsLabel" => "Masaüstü bildirimlerini göster",
        "LanguageLabel" => "Dil / Language",
        "LogLevelLabel" => "Log Seviyesi",
        "AppUpdatesHeader" => "Uygulama Güncellemeleri",
        "BtnCheckUpdates" => "Güncellemeleri Kontrol Et",
        "BtnDownloadUpdate" => "İndir ve Güncelle",
        "BtnClose" => "Kapat",
        "VersionPrefix" => "Sürüm",
        "VersionUpToDate" => "Sürümünüz güncel tutuluyor.",

        "StartWithWindowsTooltip" => "Bilgisayarınız açıldığında uygulamanın sistem tepsisinde (tray) otomatik başlamasını sağlar.",
        "ShowNotificationsTooltip" => "Senkronizasyon tamamlandığında veya hata oluştuğunda masaüstü bildirimleri görüntüler.",
        "LanguageTooltip" => "Bilgisayarınız açıldığında uygulamanın sistem tepsisinde (tray) otomatik başlamasını sağlar.",
        "LogLevelTooltip" => "Kaydedilecek log detay seviyesini ayarlar (Bilgi, Hata, Hata Ayıklama).",

        "TraySyncNow" => "Şimdi Senkronize Et",
        "TraySyncStore" => "{0} Senkronize Et",
        "TraySettings" => "Ayarlar…",
        "TrayViewLog" => "Son Log Dosyasını Aç",
        "TrayCheckUpdates" => "Güncellemeleri Kontrol Et…",
        "TrayDisconnect" => "ITAD Bağlantısını Kes",
        "TrayConnect" => "ITAD'a Bağlan",
        "TrayExit" => "Çıkış",
        "ExitConfirmText" => "Senkronizasyon devam ediyor. Yine de çıkılsın mı?",
        "ExitConfirmTitle" => "ITAD Library Sync — Çıkış",

        _ => key
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
