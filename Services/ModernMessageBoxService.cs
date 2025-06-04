using System.Windows;
using lingualink_client.Views.Components;

namespace lingualink_client.Services
{
    /// <summary>
    /// 现代化消息框服务，提供统一的消息显示接口
    /// </summary>
    public static class ModernMessageBoxService
    {
        /// <summary>
        /// 显示信息消息
        /// </summary>
        public static MessageBoxResult ShowInfo(string message, string title = "", Window? owner = null)
        {
            return ModernMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        /// <summary>
        /// 显示成功消息
        /// </summary>
        public static MessageBoxResult ShowSuccess(string message, string title = "", Window? owner = null)
        {
            var successTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("SuccessTitle") : title;
            return ModernMessageBox.Show(message, successTitle, MessageBoxButton.OK, MessageBoxImage.Information, owner);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public static MessageBoxResult ShowWarning(string message, string title = "", Window? owner = null)
        {
            var warningTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("WarningTitle") : title;
            return ModernMessageBox.Show(message, warningTitle, MessageBoxButton.OK, MessageBoxImage.Warning, owner);
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public static MessageBoxResult ShowError(string message, string title = "", Window? owner = null)
        {
            var errorTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("ErrorTitle") : title;
            return ModernMessageBox.Show(message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error, owner);
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public static MessageBoxResult ShowConfirm(string message, string title = "", Window? owner = null)
        {
            var confirmTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("ConfirmTitle") : title;
            return ModernMessageBox.Show(message, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, owner);
        }

        /// <summary>
        /// 显示是/否/取消对话框
        /// </summary>
        public static MessageBoxResult ShowYesNoCancel(string message, string title = "", Window? owner = null)
        {
            var questionTitle = string.IsNullOrEmpty(title) ? LanguageManager.GetString("QuestionTitle") : title;
            return ModernMessageBox.Show(message, questionTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, owner);
        }

        /// <summary>
        /// 显示确定/取消对话框
        /// </summary>
        public static MessageBoxResult ShowOkCancel(string message, string title = "", Window? owner = null)
        {
            return ModernMessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, owner);
        }

        /// <summary>
        /// 显示自定义消息框
        /// </summary>
        public static MessageBoxResult Show(string message, string title = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, Window? owner = null)
        {
            return ModernMessageBox.Show(message, title, button, icon, owner);
        }
    }
}
