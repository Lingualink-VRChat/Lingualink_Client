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
        private AccountPageViewModel? _viewModel;

        public AccountPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new AccountPageViewModel();
            DataContext = _viewModel;
            var viewModel = _viewModel;

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
            if (_viewModel != null)
            {
                _viewModel.CancelPendingProfileSync();
                _viewModel.Dispose();
                _viewModel = null;
            }

            DataContext = null;
        }
    }
}
