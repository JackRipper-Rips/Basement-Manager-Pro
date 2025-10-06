using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolusManifestApp.Services;
using SolusManifestApp.ViewModels;
using SolusManifestApp.Views;
using System.Windows;

namespace SolusManifestApp
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<LoggerService>();
                    services.AddSingleton<SteamService>();
                    services.AddSingleton<SteamGamesService>();
                    services.AddSingleton<SteamApiService>();
                    services.AddSingleton<ManifestApiService>();
                    services.AddSingleton<DownloadService>();
                    services.AddSingleton<FileInstallService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<UpdateService>();
                    services.AddSingleton<NotificationService>();
                    services.AddSingleton<CacheService>();
                    services.AddSingleton<BackupService>();
                    services.AddSingleton<DepotDownloadService>();
                    services.AddSingleton<SteamLibraryService>();
                    services.AddSingleton<ThemeService>();

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<HomeViewModel>();
                    services.AddTransient<LuaInstallerViewModel>();
                    services.AddTransient<LibraryViewModel>();
                    services.AddTransient<StoreViewModel>();
                    services.AddTransient<DownloadsViewModel>();
                    services.AddTransient<ToolsViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Views
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            // Load and apply theme
            var settingsService = _host.Services.GetRequiredService<SettingsService>();
            var themeService = _host.Services.GetRequiredService<ThemeService>();
            var settings = settingsService.LoadSettings();
            themeService.ApplyTheme(settings.Theme);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync();
            }

            base.OnExit(e);
        }
    }
}
