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
            // 使用新的容器ViewModel实现数据驱动模式
            DataContext = new IndexWindowViewModel();
        }
    }
}