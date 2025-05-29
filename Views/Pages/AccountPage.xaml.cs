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
    }
} 