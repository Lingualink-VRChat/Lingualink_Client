using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using lingualink_client.Services;
// 明确指定使用System.Windows的MessageBox类型
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace lingualink_client.Views.Components
{
    public partial class ModernMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private ModernMessageBox()
        {
            InitializeComponent();
            
            // 使窗口可拖拽
            MouseLeftButtonDown += (sender, e) => DragMove();
            
            // ESC键关闭
            KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Result = MessageBoxResult.Cancel;
                    Close();
                }
            };
        }

        public static MessageBoxResult Show(string message, string title = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, Window? owner = null)
        {
            var messageBox = new ModernMessageBox();
            
            // 设置所有者
            if (owner != null)
            {
                messageBox.Owner = owner;
            }
            else if (Application.Current.MainWindow != null)
            {
                messageBox.Owner = Application.Current.MainWindow;
            }

            // 设置标题
            messageBox.TitleText.Text = string.IsNullOrEmpty(title) ? GetDefaultTitle(icon) : title;
            messageBox.Title = messageBox.TitleText.Text;

            // 设置消息
            messageBox.MessageText.Text = message;

            // 设置图标
            messageBox.SetIcon(icon);

            // 设置按钮
            messageBox.SetButtons(button);

            // 显示对话框
            messageBox.ShowDialog();

            return messageBox.Result;
        }

        private static string GetDefaultTitle(MessageBoxImage icon)
        {
            return icon switch
            {
                MessageBoxImage.Error => LanguageManager.GetString("ErrorTitle"),
                MessageBoxImage.Warning => LanguageManager.GetString("WarningTitle"),
                MessageBoxImage.Question => LanguageManager.GetString("QuestionTitle"),
                MessageBoxImage.Information => LanguageManager.GetString("InfoTitle"),
                _ => LanguageManager.GetString("InfoTitle")
            };
        }

        private void SetIcon(MessageBoxImage icon)
        {
            var (symbol, color) = icon switch
            {
                MessageBoxImage.Error => (SymbolRegular.ErrorCircle24, "#E74C3C"),
                MessageBoxImage.Warning => (SymbolRegular.Warning24, "#F39C12"),
                MessageBoxImage.Question => (SymbolRegular.QuestionCircle24, "#3498DB"),
                MessageBoxImage.Information => (SymbolRegular.Info24, "#2ECC71"),
                _ => (SymbolRegular.Info24, "#2ECC71")
            };

            MessageIcon.Symbol = symbol;
            MessageIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        }

        private void SetButtons(MessageBoxButton button)
        {
            ButtonPanel.Children.Clear();

            switch (button)
            {
                case MessageBoxButton.OK:
                    AddButton(LanguageManager.GetString("OK"), MessageBoxResult.OK, true, true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton(LanguageManager.GetString("Cancel"), MessageBoxResult.Cancel, false, false);
                    AddButton(LanguageManager.GetString("OK"), MessageBoxResult.OK, true, true);
                    break;

                case MessageBoxButton.YesNo:
                    AddButton(LanguageManager.GetString("No"), MessageBoxResult.No, false, false);
                    AddButton(LanguageManager.GetString("Yes"), MessageBoxResult.Yes, true, true);
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton(LanguageManager.GetString("Cancel"), MessageBoxResult.Cancel, false, false);
                    AddButton(LanguageManager.GetString("No"), MessageBoxResult.No, false, false);
                    AddButton(LanguageManager.GetString("Yes"), MessageBoxResult.Yes, true, true);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isDefault, bool isPrimary)
        {
            var button = new Wpf.Ui.Controls.Button
            {
                Content = text,
                Width = 80,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                IsDefault = isDefault,
                Appearance = isPrimary ? ControlAppearance.Primary : ControlAppearance.Secondary
            };

            button.Click += (sender, e) =>
            {
                Result = result;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }
    }
}
