using System;
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
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Closed -= OnClosed;
            if (_disposeViewModelOnClose)
            {
                _viewModel.Dispose();
            }
        }
    }
}
