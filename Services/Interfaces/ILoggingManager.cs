using System;
using System.Collections.ObjectModel;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 日志管理器接口 - 负责应用程序的日志记录和管理
    /// </summary>
    public interface ILoggingManager
    {
        /// <summary>
        /// 日志消息集合
        /// </summary>
        ObservableCollection<string> LogMessages { get; }

        /// <summary>
        /// 格式化的日志消息字符串
        /// </summary>
        string FormattedLogMessages { get; }

        /// <summary>
        /// 添加日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        void AddMessage(string message);

        /// <summary>
        /// 清除所有日志消息
        /// </summary>
        void ClearMessages();

        /// <summary>
        /// 日志消息添加事件
        /// </summary>
        event EventHandler<string>? MessageAdded;

        /// <summary>
        /// 日志清除事件
        /// </summary>
        event EventHandler? MessagesCleared;
    }
} 