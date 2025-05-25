using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 日志管理器实现 - 提供线程安全的日志管理功能
    /// </summary>
    public class LoggingManager : ILoggingManager
    {
        private const int MaxLogEntries = 500;
        private readonly object _lockObject = new object();

        public ObservableCollection<string> LogMessages { get; }
        
        public string FormattedLogMessages => string.Join(Environment.NewLine, LogMessages);

        public event EventHandler<string>? MessageAdded;
        public event EventHandler? MessagesCleared;

        public LoggingManager()
        {
            LogMessages = new ObservableCollection<string>();
            LogMessages.CollectionChanged += OnLogMessagesChanged;
        }

        public void AddMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Debug.WriteLine($"LoggingManager.AddMessage: \"{message}\"");

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                    LogMessages.Add(timestampedMessage);

                    // 限制日志条目数量
                    while (LogMessages.Count > MaxLogEntries)
                    {
                        LogMessages.RemoveAt(0);
                    }

                    Debug.WriteLine($"LoggingManager: Message added. Total count: {LogMessages.Count}");
                }
            });

            MessageAdded?.Invoke(this, message);
        }

        public void ClearMessages()
        {
            Debug.WriteLine("LoggingManager.ClearMessages called");

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    LogMessages.Clear();
                    Debug.WriteLine($"LoggingManager: Messages cleared. Count: {LogMessages.Count}");
                }
            });

            MessagesCleared?.Invoke(this, EventArgs.Empty);
            
            // 添加清除日志的记录
            AddMessage(LanguageManager.GetString("LogCleared"));
        }

        private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 通知FormattedLogMessages属性变化
            // 注意：这里需要在实际使用时通过PropertyChanged事件通知UI更新
            Debug.WriteLine("LoggingManager: LogMessages collection changed");
        }
    }
} 