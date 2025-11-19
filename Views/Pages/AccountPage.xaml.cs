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
        public AccountPage()
        {
            InitializeComponent();
            DataContext = new AccountPageViewModel();
        }

        /// <summary>
        /// Handle PasswordBox PasswordChanged event for ApiKey to ensure proper two-way binding
        /// </summary>
        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.PasswordBox passwordBox && DataContext is AccountPageViewModel viewModel)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPage] ApiKey PasswordBox changed to: '{passwordBox.Password}'");
                viewModel.ApiKey = passwordBox.Password;
            }
        }
    }
} 
