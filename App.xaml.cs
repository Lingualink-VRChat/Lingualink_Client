using lingualink_client.ViewModels;
using lingualink_client.Services;
using System.Windows;
using System.Threading.Tasks;

namespace lingualink_client
{
    public partial class App : Application
    {
        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化服务容器 - 必须在创建任何ViewModel之前调用
            ServiceInitializer.Initialize();

            // 从设置中加载语言，而不是硬编码为中文
            var settingsService = new Services.SettingsService();
            var appSettings = settingsService.LoadSettings();
            var culture = new System.Globalization.CultureInfo(appSettings.GlobalLanguage);
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;

            SharedIndexWindowViewModel = new IndexWindowViewModel();

            // Test OGG/OPUS generation
            _ = Task.Run(async () => {
                bool success = await TestOggOpus.TestOggOpusGeneration();
                System.Diagnostics.Debug.WriteLine($"OGG/OPUS test result: {success}");
            });
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