using lingualink_client.Services;
using lingualink_client.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using lingualink_client.Models;

namespace lingualink_client.Views
{
    /// <summary>
    /// SettingPage.xaml 的交互逻辑
    /// </summary>
    public partial class SettingPage : Page
    {
        private readonly SettingPageViewModel _viewModel;

        private readonly SettingsService _settingsService;

        private AppSettings _appSettings;

        public List<CultureInfo> Languages { get; set; }

        public SettingPage()
        {
            InitializeComponent();

            this.Loaded += SettingPage_Loaded;

            _viewModel = new SettingPageViewModel();

            _settingsService = new SettingsService();

            _appSettings = _settingsService.LoadSettings();

            DataContext = _viewModel;

            Languages = LanguageManager.GetAvailableLanguages();
            LanguageComboBox.ItemsSource = Languages;

            var currentCulture = Thread.CurrentThread.CurrentUICulture;

            LanguageComboBox.SelectedItem = LanguageManager.GetAvailableLanguages()
                .FirstOrDefault(c => c.Name == _appSettings.GlobalLanguage);
        }

        private void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _appSettings = _settingsService.LoadSettings();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is CultureInfo cultureInfo)
            {
                LanguageManager.ChangeLanguage(cultureInfo.Name);

                _appSettings.GlobalLanguage = cultureInfo.Name;

                _settingsService.SaveSettings(_appSettings);
            }
        }
    }
}
