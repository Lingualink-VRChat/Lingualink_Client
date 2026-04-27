using System.Windows;
using System.Windows.Controls;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                _viewModel = new ConversationHistoryViewModel(
                    ServiceContainer.Resolve<IConversationHistoryService>(),
                    ServiceContainer.Resolve<ILoggingManager>());
            }

            DataContext = _viewModel;
        }
    }
}
