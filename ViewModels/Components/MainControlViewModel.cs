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
            _workButtonContent = LanguageManager.GetString("StartListening");

            _loggingManager.AddMessage(LanguageManager.GetString("IndexVmCtorLogInit"));

            LoadSettingsAndInitializeServices();
            SubscribeToEvents();

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void SubscribeToEvents()
        {
            // 订阅麦克风管理器PropertyChanged事件（仍需要用于UI绑定）
            _microphoneManager.PropertyChanged += OnMicrophoneManagerPropertyChanged;

            // 通过事件聚合器订阅事件
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Subscribe<ViewModels.Events.MicrophoneChangedEvent>(OnMicrophoneChanged);
            eventAggregator.Subscribe<ViewModels.Events.SettingsChangedEvent>(OnGlobalSettingsChanged);
            eventAggregator.Subscribe<ViewModels.Events.StatusUpdatedEvent>(OnOrchestratorStatusUpdated);
            eventAggregator.Subscribe<ViewModels.Events.TranslationCompletedEvent>(OnTranslationCompleted);
            eventAggregator.Subscribe<ViewModels.Events.OscMessageSentEvent>(OnOscMessageSent);
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
                ? LanguageManager.GetString("StopListening") 
                : LanguageManager.GetString("StartListening");
            
            // 更新其他本地化标签
            OnPropertyChanged(nameof(WorkHintLabel));
        }

        private void OnGlobalSettingsChanged(ViewModels.Events.SettingsChangedEvent e)
        {
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] OnGlobalSettingsChanged() called from {e.ChangeSource}");

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
                _orchestrator.Dispose();
            }

            // 创建新的协调器
            System.Diagnostics.Debug.WriteLine($"[MainControlViewModel] Creating new AudioTranslationOrchestrator");
            _orchestrator = new Services.Managers.AudioTranslationOrchestrator(_appSettings, _loggingManager);

            // 恢复工作状态
            if (wasWorking && previouslySelectedMicDeviceNumber.HasValue && 
                _microphoneManager.SelectedMicrophone?.WaveInDeviceIndex == previouslySelectedMicDeviceNumber)
            {
                if (_orchestrator.Start(previouslySelectedMicDeviceNumber.Value))
                {
                    WorkButtonContent = LanguageManager.GetString("StopListening");
                    IsMicrophoneComboBoxEnabled = false;
                }
                else
                {
                    WorkButtonContent = LanguageManager.GetString("StartListening");
                    IsMicrophoneComboBoxEnabled = true;
                }
            }
            else if (wasWorking)
            {
                WorkButtonContent = LanguageManager.GetString("StartListening");
                IsMicrophoneComboBoxEnabled = true;
            }

            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        private void OnMicrophoneChanged(ViewModels.Events.MicrophoneChangedEvent e)
        {
            var microphone = e.SelectedMicrophone;
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

        private void OnOrchestratorStatusUpdated(ViewModels.Events.StatusUpdatedEvent e)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = e.Status);
        }

        private void OnTranslationCompleted(ViewModels.Events.TranslationCompletedEvent e)
        {
            // 事件已经是正确的格式，可以直接处理或转发给其他组件
            // 这里可以添加额外的处理逻辑
        }

        private void OnOscMessageSent(ViewModels.Events.OscMessageSentEvent e)
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
                        WorkButtonContent = LanguageManager.GetString("StopListening");
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
                WorkButtonContent = LanguageManager.GetString("StartListening");
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
            _microphoneManager.PropertyChanged -= OnMicrophoneManagerPropertyChanged;

            // 取消订阅事件聚合器事件
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Unsubscribe<ViewModels.Events.MicrophoneChangedEvent>(OnMicrophoneChanged);
            eventAggregator.Unsubscribe<ViewModels.Events.SettingsChangedEvent>(OnGlobalSettingsChanged);
            eventAggregator.Unsubscribe<ViewModels.Events.StatusUpdatedEvent>(OnOrchestratorStatusUpdated);
            eventAggregator.Unsubscribe<ViewModels.Events.TranslationCompletedEvent>(OnTranslationCompleted);
            eventAggregator.Unsubscribe<ViewModels.Events.OscMessageSentEvent>(OnOscMessageSent);

            _orchestrator?.Dispose();
        }
    }
} 