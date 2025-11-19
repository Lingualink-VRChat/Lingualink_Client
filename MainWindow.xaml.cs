using lingualink_client.Services;
using lingualink_client.ViewModels;
using lingualink_client.Views;
using System.Globalization;
using System.Windows;
using Wpf.Ui.Appearance;
using Microsoft.Win32;

namespace lingualink_client
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainWindowViewModel _viewModel;

        private readonly SettingsService _settingsService;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();

            _settingsService = new SettingsService();

            DataContext = _viewModel;

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(IndexPage));

            var appSettings = _settingsService.LoadSettings();

            // 确保语言设置正确应用
            AppLanguageHelper.ApplyLanguage(appSettings);
            
            // 强制刷新UI以确保语言更改生效
            _viewModel.RefreshLanguageBindings();
            
            // 根据系统主题设置应用主题
            var systemTheme = GetSystemTheme();
            ApplicationThemeManager.Apply(systemTheme);
        }

        private ApplicationTheme GetSystemTheme()
        {
            try
            {
                // 检查系统是否使用深色主题
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var appsUseLightTheme = key?.GetValue("AppsUseLightTheme");
                
                if (appsUseLightTheme is int lightTheme)
                {
                    return lightTheme == 0 ? ApplicationTheme.Dark : ApplicationTheme.Light;
                }
            }
            catch
            {
                // 如果无法读取注册表，默认使用浅色主题
            }
            
            return ApplicationTheme.Light;
        }

        private void ThemesChanged(object sender, RoutedEventArgs e)
        {
            if (ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark)
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
            }
            else
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {

        }
    }
}
