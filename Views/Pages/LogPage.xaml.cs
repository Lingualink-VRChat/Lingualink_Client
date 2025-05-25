using System.Windows.Controls;
using lingualink_client.ViewModels.Components;

namespace lingualink_client.Views
{
    public partial class LogPage : Page
    {
        public LogPage()
        {
            InitializeComponent();
            // 使用新的数据驱动模式的LogViewModel
            DataContext = new LogViewModel();
        }
    }
}