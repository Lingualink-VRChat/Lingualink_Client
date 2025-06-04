using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.ViewModels.Managers;
// 使用现代化MessageBox替换系统默认的MessageBox
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels.Components
{
    /// <summary>
    /// 主控制ViewModel - 负责协调各个管理器和处理主要的工作流程控制
    /// </summary>
    public partial class MainControlViewModel : ViewModelBase, IDisposable
    {
        private readonly ILoggingManager _loggingManager;
        private readonly IMicrophoneManager _microphoneManager;
        private readonly SettingsService _settingsService;
        private IAudioTranslationOrchestrator? _orchestrator;
        private AppSettings _appSettings = null!;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _workButtonContent = string.Empty;

        [ObservableProperty]
        private bool _isMicrophoneComboBoxEnabled = true;

        // 本地化标签
        public string WorkHintLabel => LanguageManager.GetString("WorkHint");

        public MainControlViewModel()
        {
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();
            _microphoneManager = ServiceContainer.Resolve<IMicrophoneManager>();
            _settingsService = new SettingsService();

            // 设置初始本地化值
            _statusText = LanguageManager.GetString("StatusInitializing");
            _workButtonContent = LanguageManager.GetString("StartWork");

            _loggingManager.AddMessage(LanguageManager.GetString("IndexVmCtorLogInit"));

            LoadSettingsAndInitializeServices();
            SubscribeToEvents();

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void SubscribeToEvents()
        {
            // 订阅麦克风管理器事件
            _microphoneManager.MicrophoneChanged += OnMicrophoneChanged;
            _microphoneManager.PropertyChanged += OnMicrophoneManagerPropertyChanged;

            // 订阅全局设置变更事件
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged;
        }

        private void OnMicrophoneManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IMicrophoneManager.IsRefreshing):
                case nameof(IMicrophoneManager.SelectedMicrophone):
                    ToggleWorkCommand.NotifyCanExecuteChanged();
                    break;
            }
        }

        private void OnLanguageChanged()
        {
            // 更新动态按钮文本
            WorkButtonContent = _orchestrator?.IsWorking == true 
                ? LanguageManager.GetString("StopWork") 
                : LanguageManager.GetString("StartWork");
            
            // 更新其他本地化标签
            OnPropertyChanged(nameof(WorkHintLabel));
        }

        private void OnGlobalSettingsChanged()
        {
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] OnGlobalSettingsChanged() called");

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool wasWorking = _orchestrator?.IsWorking ?? false;
                LoadSettingsAndInitializeServices(true);

                // 只有在不工作且没有显示特定状态时才更新状态
                if (!wasWorking && !(_orchestrator?.IsWorking ?? false) &&
                    !StatusText.Contains(LanguageManager.GetString("StatusOscInitFailed").Split(':')[0]) &&
                    !StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0]))
                {
                    StatusText = LanguageManager.GetString("StatusSettingsUpdated");
                    if (!_microphoneManager.Microphones.Any() || _microphoneManager.SelectedMicrophone == null)
                    {
                        StatusText += $" {LanguageManager.GetString("StatusPleaseSelectMic")}";
                    }
                    else
                    {
                        StatusText += (_appSettings.EnableOsc)
                            ? $" {LanguageManager.GetString("StatusReadyWithOsc")}"
                            : $" {LanguageManager.GetString("StatusReadyWithoutOsc")}";
                    }
                }
                ToggleWorkCommand.NotifyCanExecuteChanged();
            });
        }

        private void LoadSettingsAndInitializeServices(bool reattachEvents = false)
        {
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] LoadSettingsAndInitializeServices() called");

            bool wasWorking = _orchestrator?.IsWorking ?? false;
            int? previouslySelectedMicDeviceNumber = wasWorking ? _microphoneManager.SelectedMicrophone?.WaveInDeviceIndex : null;

            _appSettings = _settingsService.LoadSettings();
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] Loaded settings - ApiKey: '{_appSettings.ApiKey}', ServerUrl: '{_appSettings.ServerUrl}'");

            // 释放旧的协调器
            if (_orchestrator != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] Disposing old orchestrator");
                _orchestrator.StatusUpdated -= OnOrchestratorStatusUpdated;
                _orchestrator.TranslationCompleted -= OnTranslationCompleted;
                _orchestrator.OscMessageSent -= OnOscMessageSent;
                _orchestrator.Dispose();
            }

            // 创建新的协调器
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] Creating new AudioTranslationOrchestrator");
            _orchestrator = new Services.Managers.AudioTranslationOrchestrator(_appSettings, _loggingManager);
            _orchestrator.StatusUpdated += OnOrchestratorStatusUpdated;
            _orchestrator.TranslationCompleted += OnTranslationCompleted;
            _orchestrator.OscMessageSent += OnOscMessageSent;

            // 恢复工作状态
            if (wasWorking && previouslySelectedMicDeviceNumber.HasValue && 
                _microphoneManager.SelectedMicrophone?.WaveInDeviceIndex == previouslySelectedMicDeviceNumber)
            {
                if (_orchestrator.Start(previouslySelectedMicDeviceNumber.Value))
                {
                    WorkButtonContent = LanguageManager.GetString("StopWork");
                    IsMicrophoneComboBoxEnabled = false;
                }
                else
                {
                    WorkButtonContent = LanguageManager.GetString("StartWork");
                    IsMicrophoneComboBoxEnabled = true;
                }
            }
            else if (wasWorking)
            {
                WorkButtonContent = LanguageManager.GetString("StartWork");
                IsMicrophoneComboBoxEnabled = true;
            }

            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        private void OnMicrophoneChanged(object? sender, MMDeviceWrapper? microphone)
        {
            if (microphone != null)
            {
                if (microphone.WaveInDeviceIndex != -1 && microphone.WaveInDeviceIndex < NAudio.Wave.WaveIn.DeviceCount)
                {
                    // 避免覆盖更重要的状态信息
                    if (!StatusText.Contains(LanguageManager.GetString("StatusOscEnabled").Split('(')[0]) && 
                        !StatusText.Contains(LanguageManager.GetString("StatusSettingsUpdated").Split(':')[0]) && 
                        !StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0]) && 
                        !(_orchestrator?.IsWorking ?? false))
                    {
                        StatusText = string.Format(LanguageManager.GetString("StatusMicSelected"), microphone.FriendlyName);
                    }
                }
                else
                {
                    int cbIndex = _microphoneManager.Microphones.IndexOf(microphone);
                    if (cbIndex >= 0 && cbIndex < NAudio.Wave.WaveIn.DeviceCount)
                    {
                        microphone.WaveInDeviceIndex = cbIndex;
                        StatusText = string.Format(LanguageManager.GetString("StatusMicSelectedFallback"), microphone.FriendlyName);
                    }
                    else
                    {
                        StatusText = string.Format(LanguageManager.GetString("StatusMicInvalid"), microphone.FriendlyName);
                        _microphoneManager.SelectedMicrophone = null;
                    }
                }
            }
            else if (!_microphoneManager.Microphones.Any())
            {
                StatusText = LanguageManager.GetString("StatusNoMicFoundRefreshCheck");
            }
            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        private void OnOrchestratorStatusUpdated(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = status);
        }

        private void OnTranslationCompleted(object? sender, Services.Interfaces.TranslationResultEventArgs e)
        {
            // 通过事件聚合器通知翻译结果更新
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Publish(new ViewModels.Events.TranslationCompletedEvent
            {
                TriggerReason = e.TriggerReason ?? "",
                OriginalText = e.OriginalText ?? "",
                ProcessedText = e.ProcessedText ?? "",
                ErrorMessage = e.ErrorMessage,
                Duration = e.DurationSeconds ?? 0.0
            });
        }

        private void OnOscMessageSent(object? sender, Services.Interfaces.OscMessageEventArgs e)
        {
            // 这里可以触发事件给其他组件
            // 暂时保留为空，后续可以扩展
        }

        [RelayCommand(CanExecute = nameof(CanExecuteToggleWork))]
        private async Task ToggleWorkAsync()
        {
            if (!(_orchestrator?.IsWorking ?? false))
            {
                if (_microphoneManager.SelectedMicrophone?.WaveInDeviceIndex != -1)
                {
                    bool success = false;
                    await Task.Run(() => success = _orchestrator!.Start(_microphoneManager.SelectedMicrophone!.WaveInDeviceIndex));

                    if (success)
                    {
                        WorkButtonContent = LanguageManager.GetString("StopWork");
                        IsMicrophoneComboBoxEnabled = false;
                    }
                }
                else
                {
                    MessageBox.Show(
                        LanguageManager.GetString("MsgBoxSelectValidMicContent"), 
                        LanguageManager.GetString("MsgBoxSelectValidMicTitle"), 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
            }
            else
            {
                await Task.Run(() => _orchestrator!.Stop());
                WorkButtonContent = LanguageManager.GetString("StartWork");
                StatusText = LanguageManager.GetString("StatusStopped");
                IsMicrophoneComboBoxEnabled = true;
            }

            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => 
            _microphoneManager.SelectedMicrophone != null && 
            _microphoneManager.SelectedMicrophone.WaveInDeviceIndex != -1 && 
            !_microphoneManager.IsRefreshing;

        /// <summary>
        /// 获取音频翻译协调器实例（用于组件间通信）
        /// </summary>
        public IAudioTranslationOrchestrator? GetOrchestrator() => _orchestrator;

        public void Dispose()
        {
            // 取消订阅事件
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            SettingsChangedNotifier.SettingsChanged -= OnGlobalSettingsChanged;
            _microphoneManager.MicrophoneChanged -= OnMicrophoneChanged;
            _microphoneManager.PropertyChanged -= OnMicrophoneManagerPropertyChanged;

            if (_orchestrator != null)
            {
                _orchestrator.StatusUpdated -= OnOrchestratorStatusUpdated;
                _orchestrator.TranslationCompleted -= OnTranslationCompleted;
                _orchestrator.OscMessageSent -= OnOscMessageSent;
                _orchestrator.Dispose();
            }
        }
    }
} 