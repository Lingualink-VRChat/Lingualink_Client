using lingualink_client.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows; // 新增

namespace lingualink_client.Views
{
    public partial class TextEntryPage : Page
    {
        private TextEntryPageViewModel? _viewModel;

        public TextEntryPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new TextEntryPageViewModel();
            DataContext = _viewModel;
        }

        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // [修复] 之前这里的逻辑是 Ctrl+Enter，现在改为用户更习惯的 Enter 发送，Shift+Enter 换行
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                var viewModel = _viewModel;
                if (viewModel == null)
                {
                    return;
                }

                e.Handled = true;
                if (viewModel.SendCommand.CanExecute(null))
                {
                    viewModel.SendCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// 处理文本框获得焦点事件
        /// </summary>
        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _viewModel?.HandleTextBoxFocusGained();
        }

        /// <summary>
        /// 处理文本框失去焦点事件
        /// </summary>
        private void InputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _viewModel?.HandleTextBoxFocusLost();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}

