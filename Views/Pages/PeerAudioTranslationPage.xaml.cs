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
        }
    }
}
