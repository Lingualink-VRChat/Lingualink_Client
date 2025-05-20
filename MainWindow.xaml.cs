using lingualink_client.Services;
using lingualink_client.ViewModels;
using lingualink_client.Views;
using System.Globalization;
using System.Windows;
using Wpf.Ui.Appearance;

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

            LanguageManager.ChangeLanguage(appSettings.GlobalLanguage);
        }

        private void ThemesChanged(object sender, RoutedEventArgs e)
        {
            if (ApplicationThemeManager.GetAppTheme() is ApplicationTheme.Dark)
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
