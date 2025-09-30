using lingualink_client.ViewModels;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Velopack;

namespace lingualink_client
{
    public partial class App : System.Windows.Application
    {
        private static readonly VelopackLoggerAdapter BootstrapLogger = new();

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                VelopackApp.Build()
                    .SetLogger(BootstrapLogger)
                    .Run();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Velopack bootstrap failed: {ex}");
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (ShouldSuppressAutoLaunch(e.Args))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 1. ???????
            ServiceInitializer.Initialize();

            // 2. ???????UI??
            var settingsService = new Services.SettingsService();
            var appSettings = settingsService.LoadSettings();
            LanguageManager.ChangeLanguage(appSettings.GlobalLanguage);

            // 3. ???ViewModel (???????????????ViewModel????)
            SharedIndexWindowViewModel = new IndexWindowViewModel();

            // ? Velopack ???????????????????
            if (ServiceContainer.TryResolve<ILoggingManager>(out var loggingManager) && loggingManager is not null)
            {
                BootstrapLogger.AttachLoggingManager(loggingManager);
            }

            _ = CheckForUpdatesAsync();
        }

        private static bool ShouldSuppressAutoLaunch(string[]? args)
        {
            if (args is null || args.Length == 0)
            {
                return false;
            }

            var suppressionTokens = new[]
            {
                // Squirrel-compatible
                "--squirrel-firstrun",
                "--squirrel-install",
                "--squirrel-updated",
                "--squirrel-uninstall",
                "--squirrel-obsolete",
                // Velopack aliases (if any)
                "--velopack-firstrun",
                "--velopack-install",
                "--velopack-updated",
                "--velopack-uninstall"
            };

            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                // Exact match against common tokens
                if (suppressionTokens.Any(token => string.Equals(arg, token, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // Fallback: suppress any Squirrel/Velopack lifecycle args
                if (arg.StartsWith("--squirrel-", StringComparison.OrdinalIgnoreCase) ||
                    arg.StartsWith("--velo", StringComparison.OrdinalIgnoreCase) ||
                    arg.StartsWith("--velopack-", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task CheckForUpdatesAsync()
        {
#if SELF_CONTAINED || FRAMEWORK_DEPENDENT
            if (!ServiceContainer.TryResolve<IUpdateService>(out var updateService) || updateService is null)
            {
                Debug.WriteLine("Update service unavailable.");
                return;
            }

            if (!updateService.IsSupported)
            {
                Debug.WriteLine("Update not supported on this platform.");
                return;
            }

            try
            {
                var result = await updateService.CheckForUpdatesAsync().ConfigureAwait(false);

                if (!result.IsSupported)
                {
                    Debug.WriteLine("Update feed disabled.");
                    return;
                }

                if (result.Error is not null)
                {
                    Debug.WriteLine($"Failed to check for updates: {result.Error.Message}");
                    return;
                }

                if (result.HasUpdate && result.Session is not null)
                {
                    SharedIndexWindowViewModel.SetUpdateSession(result.Session);
                }
                else
                {
                    Debug.WriteLine("No updates available.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check for updates: {ex.Message}");
            }
#else
            Debug.WriteLine("Update check skipped for non-release configuration.");
            await Task.CompletedTask;
#endif
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SharedIndexWindowViewModel?.Dispose(); // Ensure ViewModel is disposed

            // ??????
            ServiceInitializer.Cleanup();

            base.OnExit(e);
        }
    }
}