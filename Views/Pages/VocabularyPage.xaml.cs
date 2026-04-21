using System.Windows;
using System.Windows.Controls;
using lingualink_client.ViewModels;

namespace lingualink_client.Views
{
    public partial class VocabularyPage : Page
    {
        private VocabularyPageViewModel? _viewModel;

        public VocabularyPage()
        {
            InitializeComponent();
            this.Loaded += VocabularyPage_Loaded;
            this.Unloaded += VocabularyPage_Unloaded;
        }

        private void VocabularyPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new VocabularyPageViewModel();
            DataContext = _viewModel;
            _ = _viewModel.LoadVocabularyAsync();
        }

        private void VocabularyPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
