using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using lingualink_client.Services;
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
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= CreateViewModel();
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

        private static AccountPageViewModel CreateViewModel()
        {
            return ServiceContainer.TryResolve<AccountPageViewModel>(out var viewModel) && viewModel != null
                ? viewModel
                : throw new InvalidOperationException("AccountPageViewModel is not registered.");
        }
    }
}
