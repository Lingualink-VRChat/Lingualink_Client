using lingualink_client.ViewModels;
using lingualink_client.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace lingualink_client
{
    public partial class App : Application
    {
        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化服务容器
            ServiceInitializer.Initialize();

            // 2. 加载设置并设置UI语言
            var settingsService = new Services.SettingsService();
            var appSettings = settingsService.LoadSettings();
            LanguageManager.ChangeLanguage(appSettings.GlobalLanguage);

            // 3. 创建主ViewModel (现在是同步创建，异步初始化将在ViewModel内部进行)
            SharedIndexWindowViewModel = new IndexWindowViewModel();
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