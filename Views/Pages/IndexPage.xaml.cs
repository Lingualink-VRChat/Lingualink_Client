using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace lingualink_client.Views
{
    public partial class IndexPage : Page
    {
        public IndexPage()
        {
            InitializeComponent();
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
            Loaded += IndexPage_Loaded;
        }

        private void IndexPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 尝试查找父级 ScrollViewer 并禁用它，
            // 以便让 Grid 的 (*) 行高生效，实现页面内局部滚动（TextBox滚动）
            // 而不是整个页面滚动
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    break;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        }
    }
}
