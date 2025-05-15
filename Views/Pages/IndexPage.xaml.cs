using System.Windows; // Required for Application.Current
using System.Windows.Controls;

namespace lingualink_client.Views // Assuming this namespace based on your existing files
{
    public partial class IndexPage : Page
    {
        // private readonly IndexWindowViewModel _viewModel; // This field is no longer needed here

        public IndexPage()
        {
            InitializeComponent();
            // Get the shared ViewModel from App.xaml.cs
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }
    }
}