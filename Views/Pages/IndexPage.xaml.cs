using System.Windows;
using System.Windows.Controls;

namespace lingualink_client.Views
{
    public partial class IndexPage : Page
    {
        public IndexPage()
        {
            InitializeComponent();
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }


    }
}