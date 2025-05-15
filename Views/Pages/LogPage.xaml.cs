using System.Windows;
using System.Windows.Controls;

namespace lingualink_client.Views // Assuming this namespace
{
    public partial class LogPage : Page
    {
        public LogPage()
        {
            InitializeComponent();
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }
    }
}