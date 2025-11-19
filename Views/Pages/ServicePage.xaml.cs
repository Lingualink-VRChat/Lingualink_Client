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
        public ServicePage()
        {
            InitializeComponent();
            DataContext = new ServicePageViewModel();
        }
    }
}
