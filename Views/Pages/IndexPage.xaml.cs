using System.Windows; // Required for Application.Current
using System.Windows.Controls;
using lingualink_client.ViewModels;

namespace lingualink_client.Views // Assuming this namespace based on your existing files
{
    public partial class IndexPage : Page
    {
        public IndexPage()
        {
            InitializeComponent();
            // 使用App中共享的IndexWindowViewModel实例，避免重复创建
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }
    }
}