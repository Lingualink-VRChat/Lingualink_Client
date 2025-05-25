using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels.Components
{
    /// <summary>
    /// 日志ViewModel - 专门负责日志显示和管理
    /// </summary>
    public partial class LogViewModel : ViewModelBase
    {
        private readonly ILoggingManager _loggingManager;

        public ObservableCollection<string> LogMessages => _loggingManager.LogMessages;
        public string FormattedLogMessages => _loggingManager.FormattedLogMessages;

        // 本地化标签
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");

        public LogViewModel()
        {
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();

            // 订阅日志管理器事件
            _loggingManager.MessageAdded += OnLogMessageAdded;

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLogMessageAdded(object? sender, string message)
        {
            OnPropertyChanged(nameof(LogMessages));
            OnPropertyChanged(nameof(FormattedLogMessages));
        }

        private void OnLanguageChanged()
        {
            // 更新所有语言相关的标签
            OnPropertyChanged(nameof(RunningLogLabel));
            OnPropertyChanged(nameof(ClearLogLabel));
        }

        [RelayCommand]
        private void ClearLog()
        {
            _loggingManager.ClearMessages();
        }

        public void Dispose()
        {
            // 取消订阅事件
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _loggingManager.MessageAdded -= OnLogMessageAdded;
        }
    }
} 