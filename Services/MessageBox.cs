using System.Windows;
using lingualink_client.Views.Components;

namespace lingualink_client.Services
{
    /// <summary>
    /// 全局MessageBox替换类，自动使用现代化的消息框
    /// 这个类可以直接替换System.Windows.MessageBox的调用
    /// </summary>
    public static class MessageBox
    {
        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(string messageBoxText)
        {
            return ModernMessageBox.Show(messageBoxText);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return ModernMessageBox.Show(messageBoxText, caption);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return ModernMessageBox.Show(messageBoxText, caption, button);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return ModernMessageBox.Show(messageBoxText, caption, button, icon);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(Window owner, string messageBoxText)
        {
            return ModernMessageBox.Show(messageBoxText, "", MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(Window owner, string messageBoxText, string caption)
        {
            return ModernMessageBox.Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button)
        {
            return ModernMessageBox.Show(messageBoxText, caption, button, MessageBoxImage.Information, owner);
        }

        /// <summary>
        /// 显示消息框（完全兼容System.Windows.MessageBox.Show的签名）
        /// </summary>
        public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return ModernMessageBox.Show(messageBoxText, caption, button, icon, owner);
        }

        // 便捷方法（可选）
        public static MessageBoxResult ShowInfo(string message, string title = "", Window? owner = null)
        {
            return ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        public static MessageBoxResult ShowSuccess(string message, string title = "", Window? owner = null)
        {
            var successTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("SuccessTitle") : title;
            return ModernMessageBox.Show(message, successTitle, MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        public static MessageBoxResult ShowWarning(string message, string title = "", Window? owner = null)
        {
            var warningTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("WarningTitle") : title;
            return ModernMessageBox.Show(message, warningTitle, MessageBoxButton.OK, MessageBoxImage.Warning, owner);
        }

        public static MessageBoxResult ShowError(string message, string title = "", Window? owner = null)
        {
            var errorTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("ErrorTitle") : title;
            return ModernMessageBox.Show(message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error, owner);
        }

        public static MessageBoxResult ShowConfirm(string message, string title = "", Window? owner = null)
        {
            var confirmTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("ConfirmTitle") : title;
            return ModernMessageBox.Show(message, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, owner);
        }

        public static MessageBoxResult ShowYesNoCancel(string message, string title = "", Window? owner = null)
        {
            var questionTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("QuestionTitle") : title;
            return ModernMessageBox.Show(message, questionTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, owner);
        }

        public static MessageBoxResult ShowOkCancel(string message, string title = "", Window? owner = null)
        {
            return ModernMessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, owner);
        }
    }
}
