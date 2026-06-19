using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Api;
using ITAD.LibrarySync.Core.Auth;
using ITAD.LibrarySync.Core.Launchers;
using ITAD.LibrarySync.Core.Profiles;
using ITAD.LibrarySync.Core.Scheduling;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _serviceProvider = ConfigureServices();
        _trayIconService = _serviceProvider.GetRequiredService<TrayIconService>();
        _trayIconService.Initialize();

        var appSettingsStorage = _serviceProvider.GetRequiredService<AppSettingsStorage>();
        var settings = appSettingsStorage.Load();

        if (!settings.HasCompletedFirstRun)
        {
            // First-run wizard (Task 16) will set HasCompletedFirstRun when complete.
        }
        else
        {
            var scheduler = _serviceProvider.GetRequiredService<SyncScheduler>();
            scheduler.Apply(settings.ToSyncScheduleOptions());

            if (settings.SyncOnStartup)
                _ = RunStartupSyncAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        if (_serviceProvider is not null)
        {
            _serviceProvider.GetService<SyncScheduler>()?.Dispose();
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
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
            // Sync-on-startup failures are surfaced via future notification/logging tasks.
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

        services.AddSingleton<ProfileManager>();
        services.AddSingleton<ICollectionSyncService, CollectionSyncService>();
        services.AddSingleton<IWaitlistSyncService, WaitlistSyncService>();
        services.AddSingleton<IWaitlistCleanupService, WaitlistCleanupService>();

        services.AddSingleton<IReadOnlyList<ILauncherReader>>(_ => LauncherReaderFactory.CreateAll());

        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
        services.AddSingleton<SyncScheduler>();
        services.AddSingleton<OAuthFlowService>();
        services.AddSingleton<TrayIconService>();

        return services.BuildServiceProvider();
    }
}
