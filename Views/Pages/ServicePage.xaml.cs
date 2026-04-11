// File: Views/Pages/ServicePage.xaml.cs
using lingualink_client.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls; // Required for PasswordBox type

namespace lingualink_client.Views
{
    public partial class ServicePage : Page
    {
        private ServicePageViewModel? _viewModel;

        public ServicePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new ServicePageViewModel();
            DataContext = _viewModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
