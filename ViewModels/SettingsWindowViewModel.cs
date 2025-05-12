using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using lingualink_client.Models;
using System; // For Uri
using System.Net; // For IPAddress

namespace lingualink_client.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private AppSettings _originalSettings;

        public ObservableCollection<SelectableTargetLanguageViewModel> TargetLanguageItems { get; }
        public DelegateCommand AddLanguageCommand { get; }

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

        // OSC Properties
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


        public AppSettings? SavedAppSettings { get; private set; }

        private static readonly List<string> AllSupportedLanguages = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
        };
        private const int MaxTargetLanguages = 5; 

        public SettingsWindowViewModel(AppSettings currentSettings)
        {
            _originalSettings = currentSettings; // Keep a copy for comparison or revert, though not used for revert here
            TargetLanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();
            AddLanguageCommand = new DelegateCommand(ExecuteAddLanguage, CanExecuteAddLanguage);
            LoadSettingsFromModel(currentSettings);
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            TargetLanguageItems.Clear();
            ServerUrl = settings.ServerUrl;
            SilenceThresholdSeconds = settings.SilenceThresholdSeconds;
            MinVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            MaxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
            MinRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold;

            // Load OSC Settings
            EnableOsc = settings.EnableOsc;
            OscIpAddress = settings.OscIpAddress;
            OscPort = settings.OscPort;
            OscSendImmediately = settings.OscSendImmediately;
            OscPlayNotificationSound = settings.OscPlayNotificationSound;

            var languagesFromSettings = string.IsNullOrWhiteSpace(settings.TargetLanguages)
                ? new List<string>()
                : settings.TargetLanguages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim())
                                         .Where(s => AllSupportedLanguages.Contains(s)) 
                                         .Distinct() 
                                         .ToList();

            if (!languagesFromSettings.Any())
            {
                languagesFromSettings.Add(AllSupportedLanguages.FirstOrDefault() ?? "英文");
            }

            foreach (var lang in languagesFromSettings.Take(MaxTargetLanguages)) 
            {
                var newItem = new SelectableTargetLanguageViewModel(this, lang, new List<string>(AllSupportedLanguages));
                TargetLanguageItems.Add(newItem);
            }
            
            UpdateItemPropertiesAndAvailableLanguages();
            AddLanguageCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteAddLanguage(object? parameter)
        {
            if (!CanExecuteAddLanguage(parameter)) return;

            string defaultNewLang = AllSupportedLanguages.FirstOrDefault(l => !TargetLanguageItems.Any(item => item.SelectedLanguage == l))
                                    ?? AllSupportedLanguages.First(); 

            var newItem = new SelectableTargetLanguageViewModel(this, defaultNewLang, new List<string>(AllSupportedLanguages));
            TargetLanguageItems.Add(newItem);
            
            UpdateItemPropertiesAndAvailableLanguages();
            AddLanguageCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteAddLanguage(object? parameter)
        {
            return TargetLanguageItems.Count < MaxTargetLanguages;
        }

        public void RemoveLanguageItem(SelectableTargetLanguageViewModel itemToRemove)
        {
            if (TargetLanguageItems.Contains(itemToRemove))
            {
                TargetLanguageItems.Remove(itemToRemove);
                UpdateItemPropertiesAndAvailableLanguages();
                AddLanguageCommand.RaiseCanExecuteChanged();
            }
        }

        public void OnLanguageSelectionChanged(SelectableTargetLanguageViewModel changedItem)
        {
            UpdateItemPropertiesAndAvailableLanguages();
        }

        private void UpdateItemPropertiesAndAvailableLanguages()
        {
            for (int i = 0; i < TargetLanguageItems.Count; i++)
            {
                var itemVm = TargetLanguageItems[i];
                itemVm.Label = $"目标语言 {i + 1}:";
                itemVm.CanRemove = TargetLanguageItems.Count > 1; 

                var availableForThisDropdown = new ObservableCollection<string>();
                foreach (var langOption in AllSupportedLanguages)
                {
                    if (langOption == itemVm.SelectedLanguage || 
                        !TargetLanguageItems.Where(it => it != itemVm).Any(it => it.SelectedLanguage == langOption))
                    {
                        availableForThisDropdown.Add(langOption);
                    }
                }
                if (!string.IsNullOrEmpty(itemVm.SelectedLanguage) && !availableForThisDropdown.Contains(itemVm.SelectedLanguage))
                {
                    availableForThisDropdown.Add(itemVm.SelectedLanguage); 
                }
                itemVm.AvailableLanguages = availableForThisDropdown;
            }
        }

        public bool TrySaveChanges()
        {
            var selectedLangsList = TargetLanguageItems
                .Select(item => item.SelectedLanguage)
                .Where(lang => !string.IsNullOrWhiteSpace(lang) && AllSupportedLanguages.Contains(lang))
                .Distinct() 
                .ToList();

            if (!selectedLangsList.Any())
            {
                MessageBox.Show("请至少选择一个目标翻译语言。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("服务器URL无效。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (SilenceThresholdSeconds <= 0) { MessageBox.Show("静音检测阈值必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds <= 0) { MessageBox.Show("最小语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MaxVoiceDurationSeconds <= 0) { MessageBox.Show("最大语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds >= MaxVoiceDurationSeconds) { MessageBox.Show("最小语音时长必须小于最大语音时长。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinRecordingVolumeThreshold < 0.0 || MinRecordingVolumeThreshold > 1.0) { MessageBox.Show("录音音量阈值必须在 0.0 和 1.0 之间。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }

            // Validate OSC Settings
            if (EnableOsc)
            {
                if (string.IsNullOrWhiteSpace(OscIpAddress) || Uri.CheckHostName(OscIpAddress) == UriHostNameType.Unknown)
                {
                     if (!IPAddress.TryParse(OscIpAddress, out _)) // Allow IP addresses
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

            SavedAppSettings = new AppSettings
            {
                TargetLanguages = string.Join(",", selectedLangsList),
                ServerUrl = this.ServerUrl,
                SilenceThresholdSeconds = this.SilenceThresholdSeconds,
                MinVoiceDurationSeconds = this.MinVoiceDurationSeconds,
                MaxVoiceDurationSeconds = this.MaxVoiceDurationSeconds,
                MinRecordingVolumeThreshold = this.MinRecordingVolumeThreshold,
                // OSC Settings
                EnableOsc = this.EnableOsc,
                OscIpAddress = this.OscIpAddress,
                OscPort = this.OscPort,
                OscSendImmediately = this.OscSendImmediately,
                OscPlayNotificationSound = this.OscPlayNotificationSound
            };
            return true;
        }
    }
}