using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Managers;
using lingualink_client.Services.Events;
using lingualink_client.ViewModels.Events;
using lingualink_client.ViewModels.Components;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace lingualink_client.ViewModels
{
    public partial class TextEntryPageViewModel : ViewModelBase, IDisposable
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;
        private TextTranslationOrchestrator _orchestrator; // 移除 readonly
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggingManager _loggingManager; // 添加日志管理器引用
        private readonly SharedStateViewModel _sharedStateViewModel;
        private readonly MainControlViewModel _mainControlViewModel;

        // 本地化标签
        public string PageTitle => LanguageManager.GetString("MessageTyping");
        public string InputTextLabel => LanguageManager.GetString("InputTextLabel");
        public string TextEntryHint => LanguageManager.GetString("TextEntryHint");
        public string SentToVrcLabel => LanguageManager.GetString("SentToVrcLabel");
        public string SendLabel => LanguageManager.GetString("Send"); // 新增：为按钮提供本地化文本

        // 代理属性，用于同步语音控制
        public ICommand ToggleWorkCommand => _mainControlViewModel.ToggleWorkCommand;
        public string WorkButtonContent => _mainControlViewModel.WorkButtonContent;
        // 注意：这里我们不再需要 IsMicrophoneComboBoxEnabled，因为按钮的可用性由命令的CanExecute决定

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        private string _inputText = string.Empty;

        // 修改 ProcessedText，使其成为只读代理属性
        public string ProcessedText => _sharedStateViewModel.LastSentMessage;

        [ObservableProperty]
        private string _inputStatusText = string.Empty;

        [ObservableProperty]
        private bool _isInputStatusVisible = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))] // 当IsSending变化时，也应更新命令状态
        private bool _isSending = false;

        public TextEntryPageViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>(); // 解析日志管理器
            _sharedStateViewModel = ServiceContainer.Resolve<SharedStateViewModel>(); // 解析共享状态

            // 获取全局唯一的 MainControlViewModel 实例
            _mainControlViewModel = (Application.Current as App)!.SharedIndexWindowViewModel.MainControl;

            _orchestrator = new TextTranslationOrchestrator(_appSettings, _loggingManager);

            // 订阅事件
            _eventAggregator.Subscribe<SettingsChangedEvent>(OnGlobalSettingsChanged);
            LanguageManager.LanguageChanged += OnLanguageChanged;
            _sharedStateViewModel.PropertyChanged += OnSharedStatePropertyChanged;
            _mainControlViewModel.PropertyChanged += OnMainControlPropertyChanged;
        }



        // 新增事件处理器
        private void OnSharedStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SharedStateViewModel.LastSentMessage))
            {
                OnPropertyChanged(nameof(ProcessedText)); // 通知UI更新
            }
        }

        private void OnMainControlPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainControlViewModel.WorkButtonContent))
            {
                OnPropertyChanged(nameof(WorkButtonContent));

                // 如果监听停止了，隐藏输入状态提醒
                if (_mainControlViewModel.GetOrchestrator()?.IsWorking == false && IsInputStatusVisible)
                {
                    IsInputStatusVisible = false;
                    InputStatusText = string.Empty;
                }
            }
        }

        private void OnGlobalSettingsChanged(SettingsChangedEvent e)
        {
            _loggingManager.AddMessage("Settings changed, re-initializing TextEntry orchestrator...");
            _appSettings = _settingsService.LoadSettings();
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 注意：这里不再需要取消订阅 StatusUpdated 事件，因为它现在是全局事件
                _orchestrator?.Dispose();
                _orchestrator = new TextTranslationOrchestrator(_appSettings, _loggingManager);
            });
            _loggingManager.AddMessage("TextEntry orchestrator re-initialized successfully.");
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(string.Empty); // 更新所有绑定，包括 SendLabel 和 TextEntryHint
        }

        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task SendAsync()
        {
            if (!CanSend()) return;

            IsSending = true;
            var textToSend = InputText;
            InputText = string.Empty; // 立即清空输入框以提供反馈

            try
            {
                // ProcessTextAsync 现在返回最终的文本，但我们不再需要它来更新UI
                // 因为UI会通过SharedStateViewModel自动更新
                await _orchestrator.ProcessTextAsync(textToSend);
            }
            catch (Exception ex)
            {
                // [修改] 不再更新StatusText，只记录日志
                _loggingManager.AddMessage($"[TextEntryPage] Error sending text: {ex.Message}");
            }
            finally
            {
                IsSending = false;
            }
        }

        private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

        /// <summary>
        /// 处理文本框获得焦点事件
        /// </summary>
        public void HandleTextBoxFocusGained()
        {
            // 检查是否正在监听，如果是则暂停并显示提醒
            if (_mainControlViewModel.GetOrchestrator()?.IsWorking == true)
            {
                _mainControlViewModel.PauseListeningForTextInput();
                InputStatusText = LanguageManager.GetString("StatusPausedForInput");
                IsInputStatusVisible = true;
            }
        }

        /// <summary>
        /// 处理文本框失去焦点事件
        /// </summary>
        public void HandleTextBoxFocusLost()
        {
            // 如果之前显示了暂停提醒，则恢复监听并隐藏提醒
            if (IsInputStatusVisible)
            {
                _mainControlViewModel.ResumeListeningFromTextInput();
                IsInputStatusVisible = false;
                InputStatusText = string.Empty;
            }
        }

        public void Dispose()
        {
            _orchestrator?.Dispose();
            // [移除] 不再订阅 StatusUpdatedEvent
            // _eventAggregator.Unsubscribe<StatusUpdatedEvent>(OnOrchestratorStatusUpdated);
            _eventAggregator.Unsubscribe<SettingsChangedEvent>(OnGlobalSettingsChanged);
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _sharedStateViewModel.PropertyChanged -= OnSharedStatePropertyChanged;
            _mainControlViewModel.PropertyChanged -= OnMainControlPropertyChanged;
        }
    }
}
