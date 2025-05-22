using System.Collections.Generic;
using System.Linq;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using System; 
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加

namespace lingualink_client.ViewModels
{
    public partial class ServicePageViewModel : ViewModelBase // 声明为 partial
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings; 

        // SaveCommand 和 RevertCommand 将被 [RelayCommand] 生成
        // public DelegateCommand SaveCommand { get; } // 移除此行
        // public DelegateCommand RevertCommand { get; } // 移除此行

        // 语言相关的标签仍然是计算属性
        public string ServerUrlLabel => LanguageManager.GetString("ServerUrl");
        public string SilenceThresholdLabel => LanguageManager.GetString("SilenceThreshold");
        public string MinVoiceDurationLabel => LanguageManager.GetString("MinVoiceDuration");
        public string MaxVoiceDurationLabel => LanguageManager.GetString("MaxVoiceDuration");
        public string MinVolumeThresholdLabel => LanguageManager.GetString("MinVolumeThreshold");
        public string EnableOscLabel => LanguageManager.GetString("EnableOsc");
        public string OscIpAddressLabel => LanguageManager.GetString("OscIpAddress");
        public string OscPortLabel => LanguageManager.GetString("OscPort");
        public string SendImmediatelyLabel => LanguageManager.GetString("SendImmediately");
        public string PlayNotificationSoundLabel => LanguageManager.GetString("PlayNotificationSound");
        public string SaveLabel => LanguageManager.GetString("Save");
        public string RevertLabel => LanguageManager.GetString("Revert");
        public string VolumeThresholdHint => LanguageManager.GetString("VolumeThresholdHint");

        // 将属性转换为 [ObservableProperty]
        [ObservableProperty] private string _serverUrl;
        [ObservableProperty] private double _silenceThresholdSeconds;
        [ObservableProperty] private double _minVoiceDurationSeconds;
        [ObservableProperty] private double _maxVoiceDurationSeconds;
        [ObservableProperty]
        private double _minRecordingVolumeThreshold;
        [ObservableProperty] private bool _enableOsc;
        [ObservableProperty] private string _oscIpAddress;
        [ObservableProperty] private int _oscPort;
        [ObservableProperty] private bool _oscSendImmediately;
        [ObservableProperty] private bool _oscPlayNotificationSound;
        
        public ServicePageViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _currentSettings = _settingsService.LoadSettings();
            
            // 命令不再需要手动初始化
            // No need to initialize SaveCommand, RevertCommand here

            LoadSettingsFromModel(_currentSettings);

            // 订阅语言变化，更新依赖语言管理器字符串的属性
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ServerUrlLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SilenceThresholdLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MinVoiceDurationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MaxVoiceDurationLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MinVolumeThresholdLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(EnableOscLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OscIpAddressLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OscPortLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SendImmediatelyLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PlayNotificationSoundLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SaveLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RevertLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(VolumeThresholdHint));
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

        private void LoadSettingsFromModel(AppSettings settings)
        {
            ServerUrl = settings.ServerUrl;
            SilenceThresholdSeconds = settings.SilenceThresholdSeconds;
            MinVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            MaxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
            MinRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold;

            EnableOsc = settings.EnableOsc;
            OscIpAddress = settings.OscIpAddress;
            OscPort = settings.OscPort;
            OscSendImmediately = settings.OscSendImmediately;
            OscPlayNotificationSound = settings.OscPlayNotificationSound;
        }

        private bool ValidateAndBuildSettings(out AppSettings? updatedSettings)
        {
            updatedSettings = _settingsService.LoadSettings(); // Load existing settings to preserve TargetLanguages

            if (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show(LanguageManager.GetString("ValidationServerUrlInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (SilenceThresholdSeconds <= 0) { MessageBox.Show(LanguageManager.GetString("ValidationSilenceThresholdInvalid"), LanguageManager.GetString("ValidationErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return false; }
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
            
            if (updatedSettings == null) updatedSettings = new AppSettings(); // Should not happen if LoadSettings worked

            // 更新只由当前页面管理的设置
            updatedSettings.ServerUrl = this.ServerUrl;
            updatedSettings.SilenceThresholdSeconds = this.SilenceThresholdSeconds;
            updatedSettings.MinVoiceDurationSeconds = this.MinVoiceDurationSeconds;
            updatedSettings.MaxVoiceDurationSeconds = this.MaxVoiceDurationSeconds;
            updatedSettings.MinRecordingVolumeThreshold = this.MinRecordingVolumeThreshold;
            updatedSettings.EnableOsc = this.EnableOsc;
            updatedSettings.OscIpAddress = this.OscIpAddress;
            updatedSettings.OscPort = this.OscPort;
            updatedSettings.OscSendImmediately = this.OscSendImmediately;
            updatedSettings.OscPlayNotificationSound = this.OscPlayNotificationSound;
            
            return true;
        }

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void ExecuteSaveSettings() // 方法名与命令名对应，无需参数
        {
            if (ValidateAndBuildSettings(out AppSettings? updatedSettingsFromThisPage))
            {
                if (updatedSettingsFromThisPage != null)
                {
                    _settingsService.SaveSettings(updatedSettingsFromThisPage);
                    _currentSettings = updatedSettingsFromThisPage; // Update local copy with the combined settings
                    SettingsChangedNotifier.RaiseSettingsChanged();
                    MessageBox.Show(LanguageManager.GetString("SettingsSavedSuccess"), LanguageManager.GetString("SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void ExecuteRevertSettings() // 方法名与命令名对应，无需参数
        {
            // 重新加载所有设置，包括可能由 IndexPage 更改的目标语言
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings);
            MessageBox.Show(LanguageManager.GetString("SettingsReverted"), LanguageManager.GetString("InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}