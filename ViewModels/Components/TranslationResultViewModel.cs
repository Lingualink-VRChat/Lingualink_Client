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
    public partial class TranslationResultViewModel : ViewModelBase, System.IDisposable
    {
        private readonly ILoggingManager _loggingManager;
        private readonly SharedStateViewModel _sharedStateViewModel;

        [ObservableProperty]
        private string _originalText = string.Empty;

        // 修改 ProcessedText，使其成为只读代理属性
        public string ProcessedText => _sharedStateViewModel.LastSentMessage;

        public ObservableCollection<string> LogMessages => _loggingManager.LogMessages;
        public string FormattedLogMessages => _loggingManager.FormattedLogMessages;

        // 本地化标签
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");
        public string VrcOutputLabel => LanguageManager.GetString("VrcOutput");
        public string WorkHintLabel => LanguageManager.GetString("WorkHint");

        public TranslationResultViewModel()
        {
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();
            _sharedStateViewModel = ServiceContainer.Resolve<SharedStateViewModel>(); // 解析共享状态

            _loggingManager.MessageAdded += OnLogMessageAdded;
            LanguageManager.LanguageChanged += OnLanguageChanged;
            _sharedStateViewModel.PropertyChanged += OnSharedStatePropertyChanged; // 订阅共享状态变化
        }

        private void OnLogMessageAdded(object? sender, string message)
        {
            OnPropertyChanged(nameof(LogMessages));
            OnPropertyChanged(nameof(FormattedLogMessages));
        }

        // 新增事件处理器
        private void OnSharedStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SharedStateViewModel.LastSentMessage))
            {
                OnPropertyChanged(nameof(ProcessedText)); // 通知UI更新
            }
        }

        private void OnLanguageChanged()
        {
            // 更新所有语言相关的标签
            OnPropertyChanged(nameof(RunningLogLabel));
            OnPropertyChanged(nameof(ClearLogLabel));
            OnPropertyChanged(nameof(VrcOutputLabel));
            OnPropertyChanged(nameof(WorkHintLabel));
        }



        [RelayCommand]
        private void ClearLog()
        {
            _loggingManager.ClearMessages();
        }

        public void Dispose()
        {
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _loggingManager.MessageAdded -= OnLogMessageAdded;
            _sharedStateViewModel.PropertyChanged -= OnSharedStatePropertyChanged;
        }
    }
} 