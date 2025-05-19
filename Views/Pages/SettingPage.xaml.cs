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

namespace lingualink_client.Views
{
    /// <summary>
    /// SettingPage.xaml 的交互逻辑
    /// </summary>
    public partial class SettingPage : Page
    {
        private readonly SettingPageViewModel _viewModel;

        public List<CultureInfo> Languages { get; set; }

        public SettingPage()
        {
            InitializeComponent();

            _viewModel = new SettingPageViewModel();
            DataContext = _viewModel;

            Languages = LanguageManager.GetAvailableLanguages();
            LanguageComboBox.ItemsSource = Languages;

            var currentCulture = Thread.CurrentThread.CurrentUICulture;
            LanguageComboBox.SelectedItem = LanguageManager.GetAvailableLanguages()
                .FirstOrDefault(c => c.Name == currentCulture.Name);
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is CultureInfo cultureInfo)
            {
                LanguageManager.ChangeLanguage(cultureInfo.Name);
            }
        }
    }
}
