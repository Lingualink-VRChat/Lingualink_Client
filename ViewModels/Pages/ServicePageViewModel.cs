using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using lingualink_client.Models;
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加
// 使用现代化MessageBox替换系统默认的MessageBox
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class ServicePageViewModel : ViewModelBase // 声明为 partial
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings; 
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _hasPendingChanges;

        // SaveCommand 和 RevertCommand 将被 [RelayCommand] 生成
        // public DelegateCommand SaveCommand { get; } // 移除此行
        // public DelegateCommand RevertCommand { get; } // 移除此行

        // 语言相关的标签仍然是计算属性
        public string PostSpeechRecordingDurationLabel => LanguageManager.GetString("PostSpeechRecordingDuration");
        public string MinVoiceDurationLabel => LanguageManager.GetString("MinVoiceDuration");
        public string MaxVoiceDurationLabel => LanguageManager.GetString("MaxVoiceDuration");
        public string MinVolumeThresholdLabel => LanguageManager.GetString("MinVolumeThreshold");

        // 语音设置提示文本
        public string PostSpeechRecordingDurationHint => LanguageManager.GetString("PostSpeechRecordingDurationHint");
        public string MinVoiceDurationHint => LanguageManager.GetString("MinVoiceDurationHint");
        public string MaxVoiceDurationHint => LanguageManager.GetString("MaxVoiceDurationHint");
        public string EnableOscLabel => LanguageManager.GetString("EnableOsc");
        public string OscIpAddressLabel => LanguageManager.GetString("OscIpAddress");
        public string OscPortLabel => LanguageManager.GetString("OscPort");
        public string SendImmediatelyLabel => LanguageManager.GetString("SendImmediately");
        public string PlayNotificationSoundLabel => LanguageManager.GetString("PlayNotificationSound");
        public string SaveLabel => LanguageManager.GetString("Save");
        public string RevertLabel => LanguageManager.GetString("Revert");
        public string VolumeThresholdHint => LanguageManager.GetString("VolumeThresholdHint");
        public string ResetToDefaultsLabel => LanguageManager.GetString("ResetToDefaults");
        
        // 音频编码相关标签
        public string OpusComplexityLabel => LanguageManager.GetString("OpusComplexity");
        public string AudioEncodingLabel => LanguageManager.GetString("AudioEncoding");
        public string OpusComplexityHint => LanguageManager.GetString("OpusComplexityHint");

        // 新增分组标题标签
        public string VoiceRecognitionSettingsLabel => LanguageManager.GetString("VoiceRecognitionSettings");
        public string VoiceRecognitionSettingsHint => LanguageManager.GetString("VoiceRecognitionSettingsHint");
        public string VrchatIntegrationLabel => LanguageManager.GetString("VrchatIntegration");
        public string VrchatIntegrationHint => LanguageManager.GetString("VrchatIntegrationHint");
        public string AudioProcessingLabel => LanguageManager.GetString("AudioProcessing");
        public string AudioProcessingHint => LanguageManager.GetString("AudioProcessingHint");

        // 音频增强相关标签
        public string AudioEnhancementLabel => LanguageManager.GetString("AudioEnhancement");
        public string AudioEnhancementHint => LanguageManager.GetString("QuietBoostHint");
        public string EnableAudioNormalizationLabel => LanguageManager.GetString("EnableAudioNormalization");
        public string NormalizationTargetDbLabel => LanguageManager.GetString("NormalizationTargetDb");
        public string EnableQuietBoostLabel => LanguageManager.GetString("EnableQuietBoost");
        public string QuietBoostRmsThresholdLabel => LanguageManager.GetString("QuietBoostRmsThreshold");
        public string QuietBoostGainLabel => LanguageManager.GetString("QuietBoostGain");
        public string AudioNormalizationHint => LanguageManager.GetString("AudioNormalizationHint");
        public string QuietBoostRmsThresholdHint => LanguageManager.GetString("QuietBoostRmsThresholdHint");
        public string QuietBoostGainHint => LanguageManager.GetString("QuietBoostGainHint");

        // 将属性转换为 [ObservableProperty]
        [ObservableProperty] private double _postSpeechRecordingDurationSeconds;
        [ObservableProperty] private double _minVoiceDurationSeconds;
        [ObservableProperty] private double _maxVoiceDurationSeconds;
        [ObservableProperty]
        private double _minRecordingVolumeThreshold;
        [ObservableProperty] private bool _enableOsc;
        [ObservableProperty] private string _oscIpAddress = string.Empty;
        [ObservableProperty] private int _oscPort;
        [ObservableProperty] private bool _oscSendImmediately;
        [ObservableProperty] private bool _oscPlayNotificationSound;
        
        // 音频编码相关属性 - Opus始终启用，只允许调节复杂度
        [ObservableProperty] private int _opusComplexity;

        // 音频增强相关属性
        [ObservableProperty] private bool _enableAudioNormalization;
        [ObservableProperty] private double _normalizationTargetDb;
        [ObservableProperty] private bool _enableQuietBoost;
        [ObservableProperty] private double _quietBoostRmsThresholdDbFs;
        [ObservableProperty] private double _quietBoostGainDb;
        
        public ServicePageViewModel(SettingsService? settingsService = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _currentSettings = _settingsService.LoadSettings();

            // 先从模型加载设置到 ViewModel 属性
            LoadSettingsFromModel(_currentSettings);

            // 初始化自动保存计时器（轻量防抖）
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoSaveTimer.Tick += AutoSaveTimerOnTick;

            // 监听属性变更，用于触发自动保存
            PropertyChanged += OnServicePagePropertyChanged;

            // 订阅语言变化，更新依赖语言管理器字符串的属性
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PostSpeechRecordingDurationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MinVoiceDurationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MaxVoiceDurationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MinVolumeThresholdLabel));

            // 语音设置提示文本的语言变化订阅
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PostSpeechRecordingDurationHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MinVoiceDurationHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MaxVoiceDurationHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(EnableOscLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OscIpAddressLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OscPortLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SendImmediatelyLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PlayNotificationSoundLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SaveLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RevertLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ResetToDefaultsLabel));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VolumeThresholdHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OpusComplexityLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioEncodingLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OpusComplexityHint));
            
            // 新增分组标题的语言变化订阅
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VoiceRecognitionSettingsLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VoiceRecognitionSettingsHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VrchatIntegrationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VrchatIntegrationHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioProcessingLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioProcessingHint));

            // 音频增强标签的语言变化订阅
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioEnhancementLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioEnhancementHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(EnableAudioNormalizationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(NormalizationTargetDbLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(EnableQuietBoostLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(QuietBoostRmsThresholdLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(QuietBoostGainLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AudioNormalizationHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(QuietBoostRmsThresholdHint));
              LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(QuietBoostGainHint));
          }

        private void OnServicePagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null)
            {
                return;
            }

            if (!IsAutoSaveProperty(e.PropertyName))
            {
                return;
            }

            _hasPendingChanges = true;

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            _autoSaveTimer.Start();
        }

        private void AutoSaveTimerOnTick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();

            if (!_hasPendingChanges)
            {
                return;
            }

            _hasPendingChanges = false;

            SaveSettingsInternal(showConfirmation: false, changeSource: "ServicePageAutoSave");
        }

        private static bool IsAutoSaveProperty(string propertyName)
        {
            return propertyName == nameof(PostSpeechRecordingDurationSeconds)
                   || propertyName == nameof(MinVoiceDurationSeconds)
                   || propertyName == nameof(MaxVoiceDurationSeconds)
                   || propertyName == nameof(MinRecordingVolumeThreshold)
                   || propertyName == nameof(EnableOsc)
                   || propertyName == nameof(OscIpAddress)
                   || propertyName == nameof(OscPort)
                   || propertyName == nameof(OscSendImmediately)
                   || propertyName == nameof(OscPlayNotificationSound)
                   || propertyName == nameof(OpusComplexity)
                   || propertyName == nameof(EnableAudioNormalization)
                   || propertyName == nameof(NormalizationTargetDb)
                   || propertyName == nameof(EnableQuietBoost)
                   || propertyName == nameof(QuietBoostRmsThresholdDbFs)
                   || propertyName == nameof(QuietBoostGainDb);
        }

        // MinRecordingVolumeThreshold 属性的 OnChanged 回调
        partial void OnMinRecordingVolumeThresholdChanged(double oldValue, double newValue)
        {
            // 在属性值设置后，如果超出范围则进行限制，并再次通知属性变更以更新 UI
            if (newValue < 0.0 || newValue > 1.0)
            {
                _minRecordingVolumeThreshold = Math.Clamp(newValue, 0.0, 1.0); // 直接更新 backing field
                OnPropertyChanged(nameof(MinRecordingVolumeThreshold)); // 通知属性变更
            }
        }

        // OpusComplexity 属性的 OnChanged 回调
        partial void OnOpusComplexityChanged(int oldValue, int newValue)
        {
            // 限制Opus复杂度在5-10之间
            if (newValue < 5 || newValue > 10)
            {
                _opusComplexity = Math.Clamp(newValue, 5, 10);
                OnPropertyChanged(nameof(OpusComplexity));
            }
        }

        // NormalizationTargetDb 属性的 OnChanged 回调
        partial void OnNormalizationTargetDbChanged(double oldValue, double newValue)
        {
            // 限制归一化目标电平在-20dB到0dB之间
            if (newValue < -20.0 || newValue > 0.0)
            {
                _normalizationTargetDb = Math.Clamp(newValue, -20.0, 0.0);
                OnPropertyChanged(nameof(NormalizationTargetDb));
            }
        }

        // QuietBoostRmsThresholdDbFs 属性的 OnChanged 回调
        partial void OnQuietBoostRmsThresholdDbFsChanged(double oldValue, double newValue)
        {
            // 限制RMS阈值在-60dB到0dB之间
            if (newValue < -60.0 || newValue > 0.0)
            {
                _quietBoostRmsThresholdDbFs = Math.Clamp(newValue, -60.0, 0.0);
                OnPropertyChanged(nameof(QuietBoostRmsThresholdDbFs));
            }
        }

        // QuietBoostGainDb 属性的 OnChanged 回调
        partial void OnQuietBoostGainDbChanged(double oldValue, double newValue)
        {
            // 限制增益在0dB到20dB之间
            if (newValue < 0.0 || newValue > 20.0)
            {
                _quietBoostGainDb = Math.Clamp(newValue, 0.0, 20.0);
                OnPropertyChanged(nameof(QuietBoostGainDb));
            }
        }

        // PostSpeechRecordingDurationSeconds 属性的 OnChanged 回调
        partial void OnPostSpeechRecordingDurationSecondsChanged(double oldValue, double newValue)
        {
            // 限制追加录音时长在0.4秒到0.7秒之间
            if (newValue < 0.4 || newValue > 0.7)
            {
                _postSpeechRecordingDurationSeconds = Math.Clamp(newValue, 0.4, 0.7);
                OnPropertyChanged(nameof(PostSpeechRecordingDurationSeconds));
            }
        }

        // MinVoiceDurationSeconds 属性的 OnChanged 回调
        partial void OnMinVoiceDurationSecondsChanged(double oldValue, double newValue)
        {
            // 限制最小语音时长在0.4秒到0.7秒之间
            if (newValue < 0.4 || newValue > 0.7)
            {
                _minVoiceDurationSeconds = Math.Clamp(newValue, 0.4, 0.7);
                OnPropertyChanged(nameof(MinVoiceDurationSeconds));
            }
        }

        // MaxVoiceDurationSeconds 属性的 OnChanged 回调
        partial void OnMaxVoiceDurationSecondsChanged(double oldValue, double newValue)
        {
            // 限制最大语音时长在1秒到10秒之间
            if (newValue < 1.0 || newValue > 10.0)
            {
                _maxVoiceDurationSeconds = Math.Clamp(newValue, 1.0, 10.0);
                OnPropertyChanged(nameof(MaxVoiceDurationSeconds));
            }
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            PostSpeechRecordingDurationSeconds = settings.PostSpeechRecordingDurationSeconds;
            MinVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            MaxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
            MinRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold;

            EnableOsc = settings.EnableOsc;
            OscIpAddress = settings.OscIpAddress;
            OscPort = settings.OscPort;
            OscSendImmediately = settings.OscSendImmediately;
            OscPlayNotificationSound = settings.OscPlayNotificationSound;
            
            // 加载音频编码设置
            OpusComplexity = settings.OpusComplexity;

            // 加载音频增强设置
            EnableAudioNormalization = settings.EnableAudioNormalization;
            NormalizationTargetDb = settings.NormalizationTargetDb;
            EnableQuietBoost = settings.EnableQuietBoost;
            QuietBoostRmsThresholdDbFs = settings.QuietBoostRmsThresholdDbFs;
            QuietBoostGainDb = settings.QuietBoostGainDb;
        }

        private bool ValidateAndBuildSettings(out AppSettings? updatedSettings)
        {
            // 重新加载最新设置作为基础，确保不会覆盖其他页面的更改
            updatedSettings = _settingsService.LoadSettings();

            // 移除服务器URL和API密钥的验证，这些现在在账户页面处理
            
            if (PostSpeechRecordingDurationSeconds <= 0) { MessageBox.Show(LanguageManager.GetString("ValidationPostSpeechRecordingDurationInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds <= 0) { MessageBox.Show(LanguageManager.GetString("ValidationMinVoiceDurationInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MaxVoiceDurationSeconds <= 0) { MessageBox.Show(LanguageManager.GetString("ValidationMaxVoiceDurationInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds >= MaxVoiceDurationSeconds) { MessageBox.Show(LanguageManager.GetString("ValidationMinMaxVoiceDuration"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            // MinRecordingVolumeThreshold 的验证现在由 OnChanged 方法处理，这里不需要重复进行硬性检查

            if (EnableOsc)
            {
                if (string.IsNullOrWhiteSpace(OscIpAddress) || Uri.CheckHostName(OscIpAddress) == UriHostNameType.Unknown)
                {
                     if (!IPAddress.TryParse(OscIpAddress, out _)) 
                     {
                        MessageBox.Show(LanguageManager.GetString("ValidationOscIpInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                     }
                }
                if (OscPort <= 0 || OscPort > 65535)
                {
                    MessageBox.Show(LanguageManager.GetString("ValidationOscPortInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            // 验证音频编码设置
            if (OpusComplexity < 5 || OpusComplexity > 10)
            {
                MessageBox.Show(LanguageManager.GetString("ValidationOpusComplexityInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            
            if (updatedSettings == null) updatedSettings = new AppSettings(); // Should not happen if LoadSettings worked

            // 更新只由当前页面管理的设置（不包括ServerUrl和ApiKey）
            updatedSettings.PostSpeechRecordingDurationSeconds = this.PostSpeechRecordingDurationSeconds;
            updatedSettings.MinVoiceDurationSeconds = this.MinVoiceDurationSeconds;
            updatedSettings.MaxVoiceDurationSeconds = this.MaxVoiceDurationSeconds;
            updatedSettings.MinRecordingVolumeThreshold = this.MinRecordingVolumeThreshold;
            updatedSettings.EnableOsc = this.EnableOsc;
            updatedSettings.OscIpAddress = this.OscIpAddress;
            updatedSettings.OscPort = this.OscPort;
            updatedSettings.OscSendImmediately = this.OscSendImmediately;
            updatedSettings.OscPlayNotificationSound = this.OscPlayNotificationSound;
            
            // 更新音频编码设置
            updatedSettings.OpusComplexity = this.OpusComplexity;

            // 更新音频增强设置
            updatedSettings.EnableAudioNormalization = this.EnableAudioNormalization;
            updatedSettings.NormalizationTargetDb = this.NormalizationTargetDb;
            updatedSettings.EnableQuietBoost = this.EnableQuietBoost;
            updatedSettings.QuietBoostRmsThresholdDbFs = this.QuietBoostRmsThresholdDbFs;
            updatedSettings.QuietBoostGainDb = this.QuietBoostGainDb;
            
            return true;
        }

        private void SaveSettingsInternal(bool showConfirmation, string changeSource)
        {
            if (!ValidateAndBuildSettings(out AppSettings? updatedSettingsFromThisPage) || updatedSettingsFromThisPage == null)
            {
                return;
            }

            // 确保保存当前的界面语言，避免语言切换bug
            AppLanguageHelper.CaptureCurrentLanguage(updatedSettingsFromThisPage);

            _settingsService.SaveSettings(updatedSettingsFromThisPage);
            _currentSettings = updatedSettingsFromThisPage; // Update local copy with the combined settings

            _hasPendingChanges = false;
            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            // 通过事件聚合器通知设置变更
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Publish(new Services.Events.SettingsChangedEvent
            {
                Settings = updatedSettingsFromThisPage,
                ChangeSource = changeSource
            });

            if (showConfirmation)
            {
                MessageBox.Show(
                    LanguageManager.GetString("SettingsSavedSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void Save() // 方法名与命令名对应，无需参数
        {
            SaveSettingsInternal(showConfirmation: true, changeSource: "ServicePage");
        }

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void Revert() // 方法名与命令名对应，无需参数
        {
            // 重新加载所有设置，包括可能由 IndexPage 更改的目标语言
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings);
            MessageBox.Show(LanguageManager.GetString("SettingsReverted"), LanguageManager.GetString("InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 恢复默认设置
        [RelayCommand]
        private void ResetToDefaults()
        {
            var defaultSettings = new AppSettings();

            // 使用 AppSettings 中的默认值重置当前页相关设置
            PostSpeechRecordingDurationSeconds = defaultSettings.PostSpeechRecordingDurationSeconds;
            MinVoiceDurationSeconds = defaultSettings.MinVoiceDurationSeconds;
            MaxVoiceDurationSeconds = defaultSettings.MaxVoiceDurationSeconds;
            MinRecordingVolumeThreshold = defaultSettings.MinRecordingVolumeThreshold;

            EnableOsc = defaultSettings.EnableOsc;
            OscIpAddress = defaultSettings.OscIpAddress;
            OscPort = defaultSettings.OscPort;
            OscSendImmediately = defaultSettings.OscSendImmediately;
            OscPlayNotificationSound = defaultSettings.OscPlayNotificationSound;
            
            OpusComplexity = defaultSettings.OpusComplexity;

            EnableAudioNormalization = defaultSettings.EnableAudioNormalization;
            NormalizationTargetDb = defaultSettings.NormalizationTargetDb;
            EnableQuietBoost = defaultSettings.EnableQuietBoost;
            QuietBoostRmsThresholdDbFs = defaultSettings.QuietBoostRmsThresholdDbFs;
            QuietBoostGainDb = defaultSettings.QuietBoostGainDb;

            // 重置后立即保存一次
            SaveSettingsInternal(showConfirmation: true, changeSource: "ServicePageResetToDefaults");
        }
    }
}
