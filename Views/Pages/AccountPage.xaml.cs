using System.Windows;
using System.Windows.Controls;
using lingualink_client.ViewModels;
using lingualink_client.Services;
using Wpf.Ui.Controls;

namespace lingualink_client.Views
{
    /// <summary>
    /// AccountPage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountPage : Page
    {
        private readonly AccountPageViewModel _viewModel;

        public AccountPage()
        {
            InitializeComponent();
            _viewModel = new AccountPageViewModel(new SettingsService());
            DataContext = _viewModel;
        }

        /// <summary>
        /// Handle PasswordBox PasswordChanged event for ApiKey to ensure proper two-way binding
        /// </summary>
        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.PasswordBox passwordBox && _viewModel != null)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPage] ApiKey PasswordBox changed to: '{passwordBox.Password}'");
                _viewModel.ApiKey = passwordBox.Password;
            }
        }
    }
} 