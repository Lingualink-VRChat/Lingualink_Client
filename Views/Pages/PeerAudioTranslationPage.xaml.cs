using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using lingualink_client.ViewModels;

namespace lingualink_client.Views
{
    public partial class PeerAudioTranslationPage : Page
    {
        private readonly PeerAudioTranslationWindowViewModel _viewModel;

        public PeerAudioTranslationPage()
        {
            InitializeComponent();
            _viewModel = new PeerAudioTranslationWindowViewModel();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() => MessagesScrollViewer.ScrollToEnd());
        }
    }
}
