// File: Views/Pages/ServicePage.xaml.cs
using lingualink_client.Services; // For SettingsService
using lingualink_client.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls; // Required for PasswordBox type

namespace lingualink_client.Views
{
    public partial class ServicePage : Page
    {
        private readonly ServicePageViewModel _viewModel;

        public ServicePage()
        {
            InitializeComponent();
            _viewModel = new ServicePageViewModel(new SettingsService());
            DataContext = _viewModel;

            // Add this line to handle PasswordChanged event
            ApiKeyPasswordBox.PasswordChanged += ApiKeyPasswordBox_PasswordChanged;
        }

        // Add this event handler
        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is Wpf.Ui.Controls.PasswordBox pb)
            {
                _viewModel.ApiKey = pb.Password;
            }
        }
    }
}