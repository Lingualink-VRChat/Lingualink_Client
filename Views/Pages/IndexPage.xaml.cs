using lingualink_client.ViewModels;
using System.Windows.Controls;

namespace lingualink_client.Views
{
    /// <summary>
    /// IndexPage.xaml 的交互逻辑
    /// </summary>
    public partial class IndexPage : Page
    {
        private readonly IndexWindowViewModel _viewModel;

        public IndexPage()
        {
            InitializeComponent();
            _viewModel = new IndexWindowViewModel();
            DataContext = _viewModel;
        }
    }
}
