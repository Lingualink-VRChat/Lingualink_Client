using System;
using System.Collections.Specialized;
using lingualink_client.ViewModels;

namespace lingualink_client.Views
{
    public partial class PeerAudioTranslationWindow
    {
        private readonly PeerAudioTranslationWindowViewModel _viewModel;
        private readonly bool _disposeViewModelOnClose;

        public PeerAudioTranslationWindow()
            : this(new PeerAudioTranslationWindowViewModel(), disposeViewModelOnClose: true)
        {
        }

        public PeerAudioTranslationWindow(PeerAudioTranslationWindowViewModel viewModel, bool disposeViewModelOnClose)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _disposeViewModelOnClose = disposeViewModelOnClose;
            DataContext = _viewModel;
            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesChanged;
            MessagesScrollViewer.ScrollToEnd();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Loaded -= OnLoaded;
            Closed -= OnClosed;
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
            if (_disposeViewModelOnClose)
            {
                _viewModel.Dispose();
            }
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() => MessagesScrollViewer.ScrollToEnd());
        }
    }
}
