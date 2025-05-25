using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels.Components
{
    /// <summary>
    /// 翻译结果ViewModel - 专门负责翻译结果显示和日志管理
    /// </summary>
    public partial class TranslationResultViewModel : ViewModelBase
    {
        private readonly ILoggingManager _loggingManager;

        [ObservableProperty]
        private string _originalText = string.Empty;

        [ObservableProperty]
        private string _processedText = string.Empty;

        public ObservableCollection<string> LogMessages => _loggingManager.LogMessages;
        public string FormattedLogMessages => _loggingManager.FormattedLogMessages;

        // 本地化标签
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");
        public string VrcOutputLabel => LanguageManager.GetString("VrcOutput");
        public string OriginalResponseLabel => LanguageManager.GetString("OriginalResponse");
        public string WorkHintLabel => LanguageManager.GetString("WorkHint");

        public TranslationResultViewModel()
        {
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();

            // 订阅日志管理器事件
            _loggingManager.MessageAdded += OnLogMessageAdded;

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;

            // 订阅翻译完成事件
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Subscribe<ViewModels.Events.TranslationCompletedEvent>(OnTranslationCompleted);
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
            OnPropertyChanged(nameof(VrcOutputLabel));
            OnPropertyChanged(nameof(OriginalResponseLabel));
            OnPropertyChanged(nameof(WorkHintLabel));
        }

        /// <summary>
        /// 更新翻译结果
        /// </summary>
        /// <param name="originalText">原始翻译文本</param>
        /// <param name="processedText">处理后的文本（用于VRC输出）</param>
        public void UpdateTranslationResult(string originalText, string processedText)
        {
            OriginalText = originalText;
            ProcessedText = processedText;
        }

        /// <summary>
        /// 清空翻译结果
        /// </summary>
        public void ClearTranslationResult()
        {
            OriginalText = string.Empty;
            ProcessedText = string.Empty;
        }

        private void OnTranslationCompleted(ViewModels.Events.TranslationCompletedEvent e)
        {
            // 更新翻译结果显示
            UpdateTranslationResult(e.OriginalText, e.ProcessedText);
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