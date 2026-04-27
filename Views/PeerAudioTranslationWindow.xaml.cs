using System;
using lingualink_client.ViewModels;

namespace lingualink_client.Views
{
    public partial class PeerAudioTranslationWindow
    {
        private readonly PeerAudioTranslationWindowViewModel _viewModel;

        public PeerAudioTranslationWindow()
        {
            InitializeComponent();
            _viewModel = new PeerAudioTranslationWindowViewModel();
            DataContext = _viewModel;
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Closed -= OnClosed;
            _viewModel.Dispose();
        }
    }
}
