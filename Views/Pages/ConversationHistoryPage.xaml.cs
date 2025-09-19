using System.Windows;
using System.Windows.Controls;
using lingualink_client.ViewModels.Components;

namespace lingualink_client.Views
{
    public partial class ConversationHistoryPage : Page
    {
        private ConversationHistoryViewModel? _viewModel;

        public ConversationHistoryPage()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                _viewModel = new ConversationHistoryViewModel();
            }

            DataContext = _viewModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;

            if (_viewModel == null)
            {
                return;
            }

            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
