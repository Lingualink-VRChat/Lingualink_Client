using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 日志管理器实现 - 提供线程安全的日志管理功能
    /// </summary>
    public class LoggingManager : ILoggingManager
    {
        private const int MaxLogEntries = 1000;
        private readonly object _lockObject = new object();

        public ObservableCollection<LogEntry> LogEntries { get; }

        public string FormattedLogMessages => string.Join(Environment.NewLine, LogEntries.Select(entry => entry.ToDisplayString()));

        public event EventHandler<LogEntry>? EntryAdded;
        public event EventHandler? MessagesCleared;

        public LoggingManager()
        {
            LogEntries = new ObservableCollection<LogEntry>();
        }

        public void AddMessage(string message, LogLevel level = LogLevel.Info, string category = "General", string? details = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var entry = new LogEntry(level, message, category, details);

            Debug.WriteLine($"LoggingManager.AddMessage: [{entry.Level}] {entry.Category} - {entry.Message}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    LogEntries.Add(entry);

                    // 限制日志条目数量
                    while (LogEntries.Count > MaxLogEntries)
                    {
                        LogEntries.RemoveAt(0);
                    }

                    Debug.WriteLine($"LoggingManager: Message added. Total count: {LogEntries.Count}");
                }

                EntryAdded?.Invoke(this, entry);
            });
        }

        public void ClearMessages()
        {
            Debug.WriteLine("LoggingManager.ClearMessages called");

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    LogEntries.Clear();
                    Debug.WriteLine($"LoggingManager: Messages cleared. Count: {LogEntries.Count}");
                }

                MessagesCleared?.Invoke(this, EventArgs.Empty);
            });

            // 添加清除日志的记录
            AddMessage(LanguageManager.GetString("LogCleared"), LogLevel.Info, "System");
        }
    }
} 


