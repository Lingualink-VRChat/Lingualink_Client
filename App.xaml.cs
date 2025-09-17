using lingualink_client.ViewModels;
using lingualink_client.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Velopack;

namespace lingualink_client
{
    public partial class App : Application
    {
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

            // 1. 初始化服务容器
            ServiceInitializer.Initialize();

            // 2. 加载设置并设置UI语言
            var settingsService = new Services.SettingsService();
            var appSettings = settingsService.LoadSettings();
            LanguageManager.ChangeLanguage(appSettings.GlobalLanguage);

            // 3. 创建主ViewModel (现在是同步创建，异步初始化将在ViewModel内部进行)
            SharedIndexWindowViewModel = new IndexWindowViewModel();

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
            string updateUrl;

#if SELF_CONTAINED
            updateUrl = "https://download.cn-nb1.rains3.com/lingualink/stable-self-contained";
#elif FRAMEWORK_DEPENDENT
            updateUrl = "https://download.cn-nb1.rains3.com/lingualink/stable-framework-dependent";
#else
            updateUrl = string.Empty;
#endif

            if (string.IsNullOrEmpty(updateUrl))
            {
                return;
            }

            try
            {
                if (SharedIndexWindowViewModel is null)
                {
                    return;
                }

                var updateManager = new UpdateManager(updateUrl);
                var updateInfo = await updateManager.CheckForUpdate();

                if (updateInfo.ReleasesToApply.Any())
                {
                    SharedIndexWindowViewModel.SetUpdateInfo(updateManager, updateInfo);
                }
                else
                {
                    updateManager.Dispose();
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

            // 清理服务容器
            ServiceInitializer.Cleanup();

            base.OnExit(e);
        }
    }
}
