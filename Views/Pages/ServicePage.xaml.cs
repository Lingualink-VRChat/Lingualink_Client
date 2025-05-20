// File: Views/Pages/ServicePage.xaml.cs
using lingualink_client.Services; // For SettingsService
using lingualink_client.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace lingualink_client.Views
{
    public partial class ServicePage : Page
    {
        private readonly ServicePageViewModel _viewModel;

        public ServicePage()
        {
            InitializeComponent();
            // Consider using a dependency injection container for SettingsService in larger apps
            _viewModel = new ServicePageViewModel(new SettingsService());
            DataContext = _viewModel;
        }
    }
}