using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using lingualink_client.ViewModels;

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
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AccountPageViewModel viewModel)
            {
                return;
            }

            try
            {
                await viewModel.EnsureProfileFreshOnPageEnterAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPage] EnsureProfileFreshOnPageEnterAsync failed: {ex.Message}");
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountPageViewModel viewModel)
            {
                viewModel.CancelPendingProfileSync();
            }
        }
    }
}
