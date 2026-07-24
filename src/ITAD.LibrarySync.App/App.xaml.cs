using System.Net.Http;

using System.Windows;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;

using ITAD.LibrarySync.App.Launchers;

using ITAD.LibrarySync.App.Services;

using ITAD.LibrarySync.App.ViewModels;

using ITAD.LibrarySync.App.Views;

using ITAD.LibrarySync.Core.Api;

using ITAD.LibrarySync.Core.Auth;

using ITAD.LibrarySync.Core.Auth.Ea;

using ITAD.LibrarySync.Core.Auth.Xbox;

using ITAD.LibrarySync.Core.Launchers;

using ITAD.LibrarySync.Core.Launchers.Ea;

using ITAD.LibrarySync.Core.Launchers.Xbox;

using ITAD.LibrarySync.Core.Logging;

using ITAD.LibrarySync.Core.Profiles;

using ITAD.LibrarySync.Core.Scheduling;

using ITAD.LibrarySync.Core.Services;

using ITAD.LibrarySync.Core.Sync;



namespace ITAD.LibrarySync.App;



public partial class App : Application

{

    private ServiceProvider? _serviceProvider;

    private TrayIconService? _trayIconService;

    private SingleInstanceService? _singleInstance;



    protected override void OnStartup(StartupEventArgs e)

    {

        base.OnStartup(e);



        ShutdownMode = ShutdownMode.OnExplicitShutdown;



        _singleInstance = new SingleInstanceService();

        if (!_singleInstance.TryBecomePrimary(ActivateRunningInstance))

        {

            Shutdown(0);

            return;

        }



        _serviceProvider = ConfigureServices();

        _trayIconService = _serviceProvider.GetRequiredService<TrayIconService>();

        _trayIconService.Initialize();



        var appSettingsStorage = _serviceProvider.GetRequiredService<AppSettingsStorage>();

        var settings = appSettingsStorage.Load();



        if (!settings.HasCompletedFirstRun)

        {

            var wizardViewModel = _serviceProvider.GetRequiredService<FirstRunWizardViewModel>();

            wizardViewModel.WizardCompleted += (_, args) =>

                ApplyNormalStartup(appSettingsStorage.Load(), skipSyncOnStartup: args.InitialSyncRan);



            var wizard = new FirstRunWizard(wizardViewModel);

            wizard.Show();

        }

        else

        {

            ApplyNormalStartup(settings);

        }

    }



    protected override void OnExit(ExitEventArgs e)

    {

        _trayIconService?.Dispose();

        _singleInstance?.Dispose();



        if (_serviceProvider is not null)

        {

            _serviceProvider.GetService<SyncScheduler>()?.Dispose();

            _serviceProvider.Dispose();

        }



        base.OnExit(e);

    }



    private void ActivateRunningInstance() =>

        _trayIconService?.Activate();



    private void ApplyNormalStartup(AppSettings settings, bool skipSyncOnStartup = false)

    {

        _serviceProvider!.GetRequiredService<WindowsStartupService>().Apply(settings.StartWithWindows);



        var tokenStorage = _serviceProvider.GetRequiredService<TokenStorage>();

        if (tokenStorage.Load() is not null)

            _ = _serviceProvider.GetRequiredService<ItadAccountService>().RefreshAsync();



        var scheduler = _serviceProvider.GetRequiredService<SyncScheduler>();

        scheduler.Apply(settings.ToSyncScheduleOptions());



        if (settings.SyncOnStartup && !skipSyncOnStartup)
            _ = RunStartupSyncAsync();

        _ = CheckUpdateOnStartupAsync();
    }

    private async Task CheckUpdateOnStartupAsync()
    {
        try
        {
            var checker = _serviceProvider?.GetService<IUpdateCheckerService>();
            if (checker is null) return;
            var result = await checker.CheckForUpdatesAsync();
            if (result.HasUpdate)
            {
                _serviceProvider?.GetService<NotificationService>()?.ShowInfo(
                    "ITAD Library Sync — Güncelleme Mevcut",
                    $"Yeni bir sürüm mevcut ({result.LatestVersion}). İndirmek için Ayarlar'ı açın.");
            }
        }
        catch
        {
            // Ignore startup update check errors
        }
    }



    private async Task RunStartupSyncAsync()

    {

        try

        {

            var orchestrator = _serviceProvider!.GetRequiredService<ISyncOrchestrator>();

            await orchestrator.SyncAllAsync();

        }

        catch

        {

            // Sync-on-startup failures are surfaced via tray notifications and logs.

        }

    }



    private static ServiceProvider ConfigureServices()

    {

        var configuration = new ConfigurationBuilder()

            .SetBasePath(AppContext.BaseDirectory)

            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)

            .Build();



        var itadSection = configuration.GetSection("Itad");

        var itadOptions = new ItadOptions

        {

            ClientId = itadSection["ClientId"]

                ?? throw new InvalidOperationException("Itad:ClientId is missing from appsettings.json."),

            RedirectUri = itadSection["RedirectUri"]

                ?? throw new InvalidOperationException("Itad:RedirectUri is missing from appsettings.json.")

        };



        var services = new ServiceCollection();



        services.AddSingleton(itadOptions);

        services.AddSingleton<TokenStorage>();

        services.AddSingleton<ProfileTokenStorage>();

        services.AddSingleton<AppSettingsStorage>();

        services.AddSingleton<ShopIdResolver>();

        services.AddSingleton<SyncPayloadBuilder>();

        services.AddSingleton<IDelayProvider, DefaultDelayProvider>();



        services.AddSingleton<IItadApiClient>(sp =>

        {

            var httpClient = new HttpClient { BaseAddress = new Uri(ItadOptions.BaseUrl) };

            return new ItadApiClient(httpClient);

        });



        services.AddSingleton<ItadOAuthService>(sp =>

        {

            var httpClient = new HttpClient();

            return new ItadOAuthService(httpClient, sp.GetRequiredService<ItadOptions>(), sp.GetRequiredService<TokenStorage>());

        });



        services.AddSingleton(XboxOAuthOptions.CreateDefault());

        services.AddSingleton<XboxTokenStorage>();

        services.AddSingleton<XboxOAuthService>(sp =>

        {

            var httpClient = new HttpClient();

            return new XboxOAuthService(

                httpClient,

                sp.GetRequiredService<XboxOAuthOptions>(),

                sp.GetRequiredService<XboxTokenStorage>());

        });

        services.AddSingleton<XboxOAuthFlowService>();

        services.AddSingleton(EaOAuthOptions.CreateWebFallback());
        services.AddSingleton<EaTokenStorage>();
        services.AddSingleton<EaOAuthService>(sp =>
        {
            var httpClient = new HttpClient();
            return new EaOAuthService(
                httpClient,
                sp.GetRequiredService<EaOAuthOptions>(),
                sp.GetRequiredService<EaTokenStorage>());
        });
        services.AddSingleton<EaJunoClient>(sp => new EaJunoClient(
            new HttpClient(),
            sp.GetRequiredService<EaOAuthOptions>(),
            sp.GetRequiredService<EaOAuthService>()));
        services.AddSingleton<EaOnlineLibraryReader>();
        services.AddSingleton<EaReader>();
        services.AddSingleton<EaOAuthFlowService>();

        services.AddSingleton<ProfileManager>();

        services.AddSingleton<ICollectionSyncService, CollectionSyncService>();

        services.AddSingleton<IWaitlistSyncService, WaitlistSyncService>();

        services.AddSingleton<IWaitlistCleanupService, WaitlistCleanupService>();



        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();

        services.AddSingleton<DisplayCatalogClient>(sp => new DisplayCatalogClient(new HttpClient()));
        services.AddSingleton<IMicrosoftStoreCatalogClient>(sp => sp.GetRequiredService<DisplayCatalogClient>());
        services.AddSingleton<XboxStoreIdNormalizer>();
        services.AddSingleton<MicrosoftStoreSyncPayloadPreparer>();
        services.AddSingleton<EaStoreSyncPayloadPreparer>();
        services.AddSingleton<CollectionSyncFaultIsolator>();

        services.AddSingleton<StoreLicenseFilter>();

        services.AddSingleton<IXboxLibraryClient>(sp => new TitleHubClient(new HttpClient()));

        services.AddSingleton<IXboxEntitlementsClient>(sp => new XboxCollectionsClient(new HttpClient()));

        services.AddSingleton<IMicrosoftStoreLibraryReader, XboxApiLibraryReader>();

        services.AddSingleton<IReadOnlyList<ILauncherReader>>(sp =>
            LauncherReaderFactory.CreateAll(
                sp.GetRequiredService<IMicrosoftStoreLibraryReader>(),
                sp.GetRequiredService<EaReader>()));



        services.AddSingleton<FileLogger>();
        services.AddSingleton<IUnmatchedTitlesService, UnmatchedTitlesService>();
        services.AddSingleton<ICustomMappingService, CustomMappingService>();
        services.AddSingleton<ILogReaderService, LogReaderService>();
        services.AddSingleton<IUpdateCheckerService>(sp =>
            new UpdateCheckerService(new HttpClient(), sp.GetService<FileLogger>()));

        services.AddSingleton<SyncProgressService>();

        services.AddSingleton<NotificationService>();

        services.AddSingleton<SyncConfirmationService>();

        services.AddSingleton<WindowsStartupService>();

        services.AddSingleton<ItadAccountService>();

        services.AddSingleton<SyncStatusService>(sp =>

        {

            var service = new SyncStatusService(sp.GetRequiredService<AppSettingsStorage>());

            service.LoadFromSettings();

            return service;

        });

        services.AddSingleton<TrayIconService>();

        services.AddSingleton<ISyncOrchestrator, TrayAwareSyncOrchestrator>();

        services.AddSingleton<SyncScheduler>();

        services.AddSingleton<OAuthFlowService>();

        services.AddTransient<SettingsViewModel>();

        services.AddTransient<FirstRunWizardViewModel>();



        return services.BuildServiceProvider();

    }

}


