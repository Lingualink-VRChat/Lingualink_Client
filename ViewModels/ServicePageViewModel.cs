using System.Collections.ObjectModel; // Not needed for TargetLanguageItems anymore
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using System; 
using System.Net;

namespace lingualink_client.ViewModels
{
    public class ServicePageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings; 

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RevertCommand { get; }

        private string _serverUrl;
        public string ServerUrl { get => _serverUrl; set => SetProperty(ref _serverUrl, value); }
        private double _silenceThresholdSeconds;
        public double SilenceThresholdSeconds { get => _silenceThresholdSeconds; set => SetProperty(ref _silenceThresholdSeconds, value); }
        private double _minVoiceDurationSeconds;
        public double MinVoiceDurationSeconds { get => _minVoiceDurationSeconds; set => SetProperty(ref _minVoiceDurationSeconds, value); }
        private double _maxVoiceDurationSeconds;
        public double MaxVoiceDurationSeconds { get => _maxVoiceDurationSeconds; set => SetProperty(ref _maxVoiceDurationSeconds, value); }
        private double _minRecordingVolumeThreshold;
        public double MinRecordingVolumeThreshold
        {
            get => _minRecordingVolumeThreshold;
            set => SetProperty(ref _minRecordingVolumeThreshold, Math.Clamp(value, 0.0, 1.0));
        }

        private bool _enableOsc;
        public bool EnableOsc { get => _enableOsc; set => SetProperty(ref _enableOsc, value); }
        private string _oscIpAddress;
        public string OscIpAddress { get => _oscIpAddress; set => SetProperty(ref _oscIpAddress, value); }
        private int _oscPort;
        public int OscPort { get => _oscPort; set => SetProperty(ref _oscPort, value); }
        private bool _oscSendImmediately;
        public bool OscSendImmediately { get => _oscSendImmediately; set => SetProperty(ref _oscSendImmediately, value); }
        private bool _oscPlayNotificationSound;
        public bool OscPlayNotificationSound { get => _oscPlayNotificationSound; set => SetProperty(ref _oscPlayNotificationSound, value); }
        
        // Target language related properties and commands are removed

        public ServicePageViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _currentSettings = _settingsService.LoadSettings();
            
            SaveCommand = new DelegateCommand(ExecuteSaveSettings);
            RevertCommand = new DelegateCommand(ExecuteRevertSettings);

            LoadSettingsFromModel(_currentSettings);
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            // TargetLanguages no longer loaded/managed here
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

        // Removed ExecuteAddLanguage, CanExecuteAddLanguage, RemoveLanguageItem, OnLanguageSelectionChanged, UpdateItemPropertiesAndAvailableLanguages

        private bool ValidateAndBuildSettings(out AppSettings? updatedSettings)
        {
            updatedSettings = _settingsService.LoadSettings(); // Load existing settings to preserve TargetLanguages

            // No longer validating TargetLanguages here
            // if (!selectedLangsList.Any()) { ... return false; }

            if (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("服务器URL无效。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            // ... other validations remain the same ...
            if (SilenceThresholdSeconds <= 0) { MessageBox.Show("静音检测阈值必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds <= 0) { MessageBox.Show("最小语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MaxVoiceDurationSeconds <= 0) { MessageBox.Show("最大语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds >= MaxVoiceDurationSeconds) { MessageBox.Show("最小语音时长必须小于最大语音时长。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinRecordingVolumeThreshold < 0.0 || MinRecordingVolumeThreshold > 1.0) { MessageBox.Show("录音音量阈值必须在 0.0 和 1.0 之间。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }


            if (EnableOsc)
            {
                if (string.IsNullOrWhiteSpace(OscIpAddress) || Uri.CheckHostName(OscIpAddress) == UriHostNameType.Unknown)
                {
                     if (!IPAddress.TryParse(OscIpAddress, out _)) 
                     {
                        MessageBox.Show("OSC IP地址无效。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                     }
                }
                if (OscPort <= 0 || OscPort > 65535)
                {
                    MessageBox.Show("OSC端口号必须在 1-65535 之间。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            
            // Update only the settings managed by this page
            if (updatedSettings == null) updatedSettings = new AppSettings(); // Should not happen if LoadSettings worked

            // updatedSettings.TargetLanguages remains as loaded from file (managed by IndexPage now)
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

        private void ExecuteSaveSettings(object? parameter)
        {
            if (ValidateAndBuildSettings(out AppSettings? updatedSettingsFromThisPage))
            {
                if (updatedSettingsFromThisPage != null)
                {
                    _settingsService.SaveSettings(updatedSettingsFromThisPage);
                    _currentSettings = updatedSettingsFromThisPage; // Update local copy with the combined settings
                    SettingsChangedNotifier.RaiseSettingsChanged();
                    MessageBox.Show("服务相关设置已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ExecuteRevertSettings(object? parameter)
        {
            // Reload all settings, including TargetLanguages potentially changed by IndexPage
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings); // This will only load service-specific parts into UI
            MessageBox.Show("更改已撤销，设置已从上次保存的状态重新加载。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}