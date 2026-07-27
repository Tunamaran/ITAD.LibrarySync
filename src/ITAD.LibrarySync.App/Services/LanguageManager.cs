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
        "BtnConnectXbox" => "Connect Xbox Account",
        "BtnDisconnectXbox" => "Disconnect Xbox Account",
        "EaAccountHeader" => "EA App Account",
        "BtnConnectEa" => "Connect EA Account",
        "BtnDisconnectEa" => "Disconnect EA Account",

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

        // ── Notifications ──
        "NotifSyncComplete" => "Sync complete",
        "NotifSyncFailed" => "Sync failed",
        "NotifSyncPartial" => "Sync completed with errors",
        "NotifNoLaunchers" => "No launchers were synced.",
        "NotifPartialFormat" => "{0} of {1} launchers synced successfully.",
        "NotifConnectedTitle" => "Connected to ITAD",
        "NotifConnectedBody" => "Successfully connected to IsThereAnyDeal.",
        "NotifConnectionFailed" => "Connection failed",
        "NotifDisconnectedTitle" => "Disconnected from ITAD",
        "NotifDisconnectedBody" => "Your ITAD account has been disconnected.",
        "NotifTokenExpiredTitle" => "ITAD session expired",
        "NotifTokenExpiredBody" => "Your IsThereAnyDeal session has expired. Connect again from the tray menu.",
        "NotifReconnectXbox" => "Reconnect Xbox in Settings",
        "NotifUnknownError" => "Unknown error",
        "NotifCollectionLabel" => "collection",
        "NotifWaitlistLabel" => "waitlist",

        // ── Tray Tooltips ──
        "TrayTooltipSyncing" => "ITAD Library Sync — Syncing…",
        "TrayTooltipSuccess" => "ITAD Library Sync — Last sync successful",
        "TrayTooltipPartial" => "ITAD Library Sync — Last sync completed with errors",
        "TrayTooltipError" => "ITAD Library Sync — Last sync failed",
        "TrayTooltipIdle" => "ITAD Library Sync — Idle",
        "TrayUpdateAvailableText" => "A new update is available ({0})!\n\nWould you like to open the download page?",
        "TrayUpdateAvailableTitle" => "Update Available",
        "TrayUpToDateText" => "Your app is on the latest version ({0}).",
        "TrayUpdateCheckTitle" => "Update Check",
        "TrayUpdateCheckFailedText" => "Failed to check for updates: {0}",
        "ErrorTitle" => "Error",
        "CouldNotOpenSettings" => "Could Not Open Settings",

        // ── First Run Wizard ──
        "WizardTitle" => "ITAD Library Sync — Setup Wizard",
        "WizardStepPrefix" => "Step",
        "WizardStepSuffix" => "of 4",
        "WizardWelcomeTitle" => "Welcome",
        "WizardItadTitle" => "Connect ITAD Account",
        "WizardPlatformTitle" => "Platform Detection",
        "WizardReadyTitle" => "Ready to Sync",
        "WizardWelcomeBody" => "Welcome to ITAD Library Sync. This wizard will help you connect your IsThereAnyDeal account, detect game platforms on your system, and run the first sync.",
        "WizardWelcomeSubtext" => "The app runs in the system tray. After setup, it will automatically sync your Epic Games, Ubisoft Connect, Battle.net, EA App and Xbox libraries.",
        "WizardItadBody" => "Connect your IsThereAnyDeal account to sync your libraries.",
        "WizardItadStatusLabel" => "Connection Status:",
        "WizardItadAccountLabel" => "Account:",
        "WizardItadConnectBtn" => "Connect to ITAD",
        "WizardItadNote" => "An authorization window will open in your browser. You must connect your ITAD account before proceeding.",
        "WizardConnected" => "Connected",
        "WizardNotConnected" => "Not Connected",
        "WizardPlatformBody" => "Scanning installed platforms. Results will be displayed below.",
        "WizardColPlatform" => "Platform",
        "WizardColStatus" => "Status",
        "WizardColGames" => "Games Found",
        "WizardReadyBody" => "Setup complete! ITAD Library Sync will run in the system tray.",
        "WizardSyncOnFinish" => "Sync now on finish",
        "WizardSyncOnFinishNote" => "When checked, all active platforms will be synced to your ITAD Collection and Waitlist profiles as soon as the wizard finishes.",
        "WizardBtnBack" => "Back",
        "WizardBtnNext" => "Next",
        "WizardBtnFinish" => "Finish",
        "WizardConnectionFailed" => "Connection Failed",
        "WizardSetupFailed" => "Setup Failed",

        // ── Fix Match Window ──
        "FixMatchTitle" => "Fix Match",
        "FixMatchHeader" => "Fix Match",
        "FixMatchDesc" => "Enter the correct ITAD Game ID or Store ID for this game that could not be automatically found in the ITAD catalog.",
        "FixMatchGameName" => "Game Title: {0}",
        "FixMatchStoreId" => "Local Store ID: {0}",
        "FixMatchPlatform" => "Platform: {0}",
        "FixMatchTargetLabel" => "Target ITAD / Store ID:",
        "FixMatchTargetTooltip" => "e.g. 018d937f-...",
        "FixMatchSave" => "Save",
        "FixMatchCancel" => "Cancel",
        "FixMatchValidation" => "Please enter a valid ITAD / Store ID.",
        "FixMatchValidationTitle" => "Warning",

        // ── Sync Progress Window ──
        "SyncProgressTitle" => "ITAD Library Sync — Sync Progress",
        "SyncProgressClose" => "Close",
        "SyncProgressSyncing" => "Syncing libraries…",
        "SyncProgressNothing" => "Sync finished — nothing to sync.",
        "SyncProgressSuccess" => "Sync completed — {0}/{1} launcher(s) succeeded.",
        "SyncProgressFailed" => "Sync failed — see log for details.",
        "SyncProgressPartial" => "Sync completed with errors — {0}/{1} launcher(s) succeeded.",

        // ── Library Preview Window ──
        "LibPreviewTitleFormat" => "{0} — Library Preview",
        "LibPreviewSummary" => "Summary: {0}",
        "LibPreviewPath" => "📌 Resolved Path: {0}",
        "LibPreviewMethod" => "🔍 Detection Method: {0}",
        "LibPreviewSearch" => "Search / Filter",
        "LibPreviewSearchTooltip" => "Search by game title or store ID",
        "LibPreviewOwnedTab" => "Owned ({0})",
        "LibPreviewWishlistTab" => "Wishlist ({0})",
        "LibPreviewGameTitle" => "Game Title",
        "LibPreviewStoreId" => "Store ID",

        // ── SettingsViewModel Dialogs ──
        "VMClearUnmatchedConfirm" => "Clear the unmatched games list?",
        "VMClearUnmatchedTitle" => "Clear List",
        "VMUpdateChecking" => "Checking for updates…",
        "VMUpdateAvailable" => "New version available: {0}",
        "VMUpdateAvailablePrompt" => "New version available ({0})!\n\nWould you like to download and update the application now?",
        "VMUpdateAvailableTitle" => "New Version Available",
        "VMUpToDate" => "You are on the latest version ({0}).",
        "VMUpdateCheckFailed" => "Update check failed: {0}",
        "VMUpdateDownloading" => "Downloading update…",
        "VMUpdateComplete" => "Download complete, restarting application…",
        "VMUpdateDownloadFailed" => "Update download failed: {0}",
        "VMXboxConnectPrompt" => "Connect Xbox account now?",
        "VMXboxConnectTitle" => "Xbox Not Connected",
        "VMEaConnectPrompt" => "Connect your EA account now?",
        "VMEaConnectTitle" => "EA Not Connected",

        // ── App.xaml.cs Error Messages ──
        "AppErrorUnexpected" => "An unexpected error occurred:\n{0}",
        "AppErrorTitle" => "ITAD Library Sync — Error",
        "AppErrorStartup" => "An error occurred while starting the application:\n{0}",
        "AppUpdateNotifTitle" => "ITAD Library Sync — Update Available",
        "AppUpdateNotifBody" => "A new version is available ({0}). Open Settings to download.",

        // ── Unmatched Reason Strings ──
        "UnmatchedReasonNotInCatalog" => "Not found in ITAD catalog",
        "UnmatchedReasonTrackingId" => "Using tracking ID (not matched)",
        "UnmatchedReasonNoApiMatch" => "No match from ITAD lookup",

        // ── Custom Mappings ──
        "CustomMappingsGuideText" => "Games you have manually matched are listed here. You can remove a mapping to let it be re-evaluated on the next sync.",
        "ColMappedId" => "Mapped ID",
        "BtnRemoveMapping" => "Remove this mapping",

        // ── FixMatch Help ──
        "FixMatchHelpPrefix" => "Search for the game on ",
        "FixMatchHelpSuffix" => " and copy the game's slug or ID from the URL.",

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
        "BtnConnectXbox" => "Xbox Hesabını Bağla",
        "BtnDisconnectXbox" => "Xbox Hesabını Kopar",
        "EaAccountHeader" => "EA App Hesabı",
        "BtnConnectEa" => "EA Hesabını Bağla",
        "BtnDisconnectEa" => "EA Hesabını Kopar",

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
        "LanguageTooltip" => "Uygulama görüntüleme dilini seçin (English / Türkçe).",
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

        // ── Bildirimler ──
        "NotifSyncComplete" => "Senkronizasyon tamamlandı",
        "NotifSyncFailed" => "Senkronizasyon başarısız",
        "NotifSyncPartial" => "Senkronizasyon hatalarla tamamlandı",
        "NotifNoLaunchers" => "Hiçbir platform senkronize edilmedi.",
        "NotifPartialFormat" => "{1} platformdan {0} tanesi başarıyla senkronize edildi.",
        "NotifConnectedTitle" => "ITAD'a Bağlandı",
        "NotifConnectedBody" => "IsThereAnyDeal hesabına başarıyla bağlanıldı.",
        "NotifConnectionFailed" => "Bağlantı başarısız",
        "NotifDisconnectedTitle" => "ITAD Bağlantısı Kesildi",
        "NotifDisconnectedBody" => "ITAD hesabınızın bağlantısı kesildi.",
        "NotifTokenExpiredTitle" => "ITAD oturumu sona erdi",
        "NotifTokenExpiredBody" => "IsThereAnyDeal oturumunuz sona erdi. Tray menüsünden tekrar bağlanın.",
        "NotifReconnectXbox" => "Ayarlar'dan Xbox'ı yeniden bağlayın",
        "NotifUnknownError" => "Bilinmeyen hata",
        "NotifCollectionLabel" => "koleksiyon",
        "NotifWaitlistLabel" => "istek listesi",

        // ── Tray Tooltip'leri ──
        "TrayTooltipSyncing" => "ITAD Library Sync — Senkronize ediliyor…",
        "TrayTooltipSuccess" => "ITAD Library Sync — Son senkronizasyon başarılı",
        "TrayTooltipPartial" => "ITAD Library Sync — Son senkronizasyon hatalarla tamamlandı",
        "TrayTooltipError" => "ITAD Library Sync — Son senkronizasyon başarısız",
        "TrayTooltipIdle" => "ITAD Library Sync — Boşta",
        "TrayUpdateAvailableText" => "Yeni bir güncelleme mevcut ({0})!\n\nİndirme sayfasını açmak ister misiniz?",
        "TrayUpdateAvailableTitle" => "Güncelleme Mevcut",
        "TrayUpToDateText" => "Uygulamanız en güncel sürümde ({0}).",
        "TrayUpdateCheckTitle" => "Güncelleme Kontrolü",
        "TrayUpdateCheckFailedText" => "Güncelleme kontrolü yapılamadı: {0}",
        "ErrorTitle" => "Hata",
        "CouldNotOpenSettings" => "Ayarlar Açılamadı",

        // ── İlk Kurulum Sihirbazı ──
        "WizardTitle" => "ITAD Library Sync — Kurulum Sihirbazı",
        "WizardStepPrefix" => "Adım",
        "WizardStepSuffix" => "/ 4",
        "WizardWelcomeTitle" => "Hoş Geldiniz",
        "WizardItadTitle" => "ITAD Hesabını Bağla",
        "WizardPlatformTitle" => "Platform Tespiti",
        "WizardReadyTitle" => "Senkronizasyona Hazır",
        "WizardWelcomeBody" => "ITAD Library Sync uygulamasına hoş geldiniz. Bu sihirbaz IsThereAnyDeal hesabınızı bağlamanıza, sisteminizdeki oyun platformlarını tespit etmeye ve ilk senkronizasyonu çalıştırmanıza yardımcı olacaktır.",
        "WizardWelcomeSubtext" => "Uygulama sistem tepsisinde (System Tray) çalışır. Kurulum tamamlandıktan sonra Epic Games, Ubisoft Connect, Battle.net, EA App ve Xbox kütüphanelerinizi otomatik senkronize tutar.",
        "WizardItadBody" => "Kütüphanelerinizi aktarabilmek için IsThereAnyDeal hesabınızı bağlayın.",
        "WizardItadStatusLabel" => "Bağlantı Durumu:",
        "WizardItadAccountLabel" => "Hesap:",
        "WizardItadConnectBtn" => "ITAD'a Bağlan",
        "WizardItadNote" => "Tarayıcıda bir yetkilendirme penceresi açılacaktır. Devam etmeden önce ITAD hesabınızı bağlamalısınız.",
        "WizardConnected" => "Bağlandı",
        "WizardNotConnected" => "Bağlı Değil",
        "WizardPlatformBody" => "Yüklü platformlar taranıyor. Sonuçlar aşağıda görüntülenecektir.",
        "WizardColPlatform" => "Platform",
        "WizardColStatus" => "Durum",
        "WizardColGames" => "Bulunan Oyunlar",
        "WizardReadyBody" => "Kurulum tamamlandı! ITAD Library Sync sistem tepsisinde çalışacaktır.",
        "WizardSyncOnFinish" => "Şimdi senkronize et",
        "WizardSyncOnFinishNote" => "İşaretlendiğinde, kurulum biter bitmez tüm aktif platformlar ITAD Koleksiyon ve İstek Listesi profillerinize aktarılacaktır.",
        "WizardBtnBack" => "Geri",
        "WizardBtnNext" => "İleri",
        "WizardBtnFinish" => "Tamamla",
        "WizardConnectionFailed" => "Bağlantı Başarısız",
        "WizardSetupFailed" => "Kurulum Başarısız",

        // ── Eşleştirme Düzeltme Penceresi ──
        "FixMatchTitle" => "Eşleştirmeyi Düzelt",
        "FixMatchHeader" => "Eşleştirmeyi Düzelt",
        "FixMatchDesc" => "ITAD kataloğunda otomatik bulunamayan bu oyun için doğru ITAD Game ID veya Mağaza ID değerini girin.",
        "FixMatchGameName" => "Oyun Adı: {0}",
        "FixMatchStoreId" => "Yerel Mağaza ID: {0}",
        "FixMatchPlatform" => "Platform: {0}",
        "FixMatchTargetLabel" => "Hedef ITAD / Mağaza ID:",
        "FixMatchTargetTooltip" => "Örn: 018d937f-...",
        "FixMatchSave" => "Kaydet",
        "FixMatchCancel" => "İptal",
        "FixMatchValidation" => "Lütfen geçerli bir ITAD / Mağaza ID girin.",
        "FixMatchValidationTitle" => "Uyarı",

        // ── Senkronizasyon İlerleme Penceresi ──
        "SyncProgressTitle" => "ITAD Library Sync — Senkronizasyon İlerlemesi",
        "SyncProgressClose" => "Kapat",
        "SyncProgressSyncing" => "Kütüphaneler senkronize ediliyor…",
        "SyncProgressNothing" => "Senkronizasyon tamamlandı — aktarılacak platform bulunamadı.",
        "SyncProgressSuccess" => "Senkronizasyon tamamlandı — {0}/{1} platform başarılı.",
        "SyncProgressFailed" => "Senkronizasyon başarısız — detaylar için günlüğe bakın.",
        "SyncProgressPartial" => "Senkronizasyon hatalarla tamamlandı — {0}/{1} platform başarılı.",

        // ── Kütüphane Önizleme Penceresi ──
        "LibPreviewTitleFormat" => "{0} — Kütüphane Önizlemesi",
        "LibPreviewSummary" => "Özet: {0}",
        "LibPreviewPath" => "📌 Tespit Edilen Yol: {0}",
        "LibPreviewMethod" => "🔍 Tespit Metodu: {0}",
        "LibPreviewSearch" => "Arama / Filtreleme",
        "LibPreviewSearchTooltip" => "Oyun adı veya mağaza ID ara",
        "LibPreviewOwnedTab" => "Sahip Olunanlar ({0})",
        "LibPreviewWishlistTab" => "İstek Listesi ({0})",
        "LibPreviewGameTitle" => "Oyun Adı",
        "LibPreviewStoreId" => "Mağaza ID",

        // ── SettingsViewModel Diyalogları ──
        "VMClearUnmatchedConfirm" => "Eşleşmeyen oyunlar listesi temizlensin mi?",
        "VMClearUnmatchedTitle" => "Listeyi Temizle",
        "VMUpdateChecking" => "Güncellemeler kontrol ediliyor…",
        "VMUpdateAvailable" => "Yeni sürüm mevcut: {0}",
        "VMUpdateAvailablePrompt" => "Yeni sürüm mevcut ({0})!\n\nŞimdi arka planda indirip uygulamayı güncellemek ister misiniz?",
        "VMUpdateAvailableTitle" => "Yeni Sürüm Mevcut",
        "VMUpToDate" => "En güncel sürümü kullanıyorsunuz ({0}).",
        "VMUpdateCheckFailed" => "Güncelleme kontrolü başarısız: {0}",
        "VMUpdateDownloading" => "Güncelleme indiriliyor…",
        "VMUpdateComplete" => "İndirme tamamlandı, uygulama yeniden başlatılıyor…",
        "VMUpdateDownloadFailed" => "Güncelleme indirmesi başarısız: {0}",
        "VMXboxConnectPrompt" => "Xbox hesabınızı şimdi bağlamak ister misiniz?",
        "VMXboxConnectTitle" => "Xbox Bağlı Değil",
        "VMEaConnectPrompt" => "EA hesabınızı şimdi bağlamak ister misiniz?",
        "VMEaConnectTitle" => "EA Bağlı Değil",

        // ── App.xaml.cs Hata Mesajları ──
        "AppErrorUnexpected" => "Uygulamada beklenmeyen bir hata oluştu:\n{0}",
        "AppErrorTitle" => "ITAD Library Sync — Hata",
        "AppErrorStartup" => "Uygulama başlatılırken bir hata oluştu:\n{0}",
        "AppUpdateNotifTitle" => "ITAD Library Sync — Güncelleme Mevcut",
        "AppUpdateNotifBody" => "Yeni bir sürüm mevcut ({0}). İndirmek için Ayarlar'ı açın.",

        // ── Eşleşmeyen Neden Stringleri ──
        "UnmatchedReasonNotInCatalog" => "ITAD kataloğunda bulunamadı",
        "UnmatchedReasonTrackingId" => "Takip ID kullanılıyor (eşleşmedi)",
        "UnmatchedReasonNoApiMatch" => "ITAD sorgulamasında eşleşme yok",

        // ── Özel Eşleştirmeler ──
        "CustomMappingsGuideText" => "Manuel olarak eşleştirdiğiniz oyunlar burada listelenir. Bir eşleştirmeyi kaldırırsanız, sonraki senkronda yeniden değerlendirilir.",
        "ColMappedId" => "Eşlenen ID",
        "BtnRemoveMapping" => "Bu eşleştirmeyi kaldır",

        // ── Eşleştirme Yardımı ──
        "FixMatchHelpPrefix" => "Oyunu ",
        "FixMatchHelpSuffix" => " sitesinde arayıp URL'deki slug veya ID değerini kopyalayın.",

        _ => key
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
