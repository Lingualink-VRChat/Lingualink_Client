using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using System.Diagnostics;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace lingualink_client.ViewModels
{
    public partial class IndexWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly MicrophoneManager _microphoneManager;
        private AudioService _audioService;
        private TranslationService _translationService;
        private readonly SettingsService _settingsService;
        private OscService? _oscService;
        private AppSettings _appSettings;
        
        // Target Language Properties
        public ObservableCollection<SelectableTargetLanguageViewModel> TargetLanguageItems { get; }

        private static readonly List<string> AllSupportedLanguages = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
        };
        private const int MaxTargetLanguages = 5;

        // Log Properties
        public ObservableCollection<string> LogMessages { get; }
        public string FormattedLogMessages => string.Join(Environment.NewLine, LogMessages);

        private const int MaxLogEntries = 500;

        // 将属性转换为 [ObservableProperty]
        [ObservableProperty] private ObservableCollection<MMDeviceWrapper> _microphones = new ObservableCollection<MMDeviceWrapper>();

        [ObservableProperty]
        private MMDeviceWrapper? _selectedMicrophone;

        [ObservableProperty] private string _statusText; // 在构造函数中初始化
        [ObservableProperty] private string _translationResultText = string.Empty;
        [ObservableProperty] private string _workButtonContent; // 在构造函数中初始化
        [ObservableProperty] private bool _isMicrophoneComboBoxEnabled = true;

        [ObservableProperty]
        private bool _isRefreshingMicrophones = false;

        // Language-dependent labels
        // Removed WorkButtonContentLabel as it's now dynamic _workButtonContent
        public string SelectMicrophoneLabel => LanguageManager.GetString("SelectMicrophone");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string RefreshingMicrophonesLabel => LanguageManager.GetString("RefreshingMicrophones");
        public string TargetLanguagesLabel => LanguageManager.GetString("TargetLanguages");
        public string AddLanguageLabel => LanguageManager.GetString("AddLanguage");
        public string RemoveLabel => LanguageManager.GetString("Remove");
        public string WorkHintLabel => LanguageManager.GetString("WorkHint");
        // Added for LogPage
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");


        public IndexWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService();
            
            TargetLanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();

            LogMessages = new ObservableCollection<string>();
            LogMessages.CollectionChanged += OnLogMessagesChanged;
            
            // Set initial localized values
            _statusText = LanguageManager.GetString("StatusInitializing");
            _workButtonContent = LanguageManager.GetString("StartWork");

            AddLogMessage(LanguageManager.GetString("IndexVmCtorLogInit"));
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IndexWindowViewModel Constructor: Log Initialized. Count: {LogMessages.Count}");

            LoadSettingsAndInitializeServices(); 
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged;

            _ = RefreshMicrophonesAsync();

            // Subscribe to language changes, update properties that depend on LanguageManager strings
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SelectMicrophoneLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RefreshLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RefreshingMicrophonesLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(TargetLanguagesLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AddLanguageLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RemoveLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(WorkHintLabel));
            // Added for LogPage
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RunningLogLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ClearLogLabel));
            // Update dynamic button text on language change
            LanguageManager.LanguageChanged += () => WorkButtonContent = _audioService.IsWorking ? LanguageManager.GetString("StopWork") : LanguageManager.GetString("StartWork");
        }

        private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FormattedLogMessages));
        }
        
        private void OnGlobalSettingsChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool wasWorking = _audioService?.IsWorking ?? false;
                LoadSettingsAndInitializeServices(true); // This will reload _appSettings and re-init services

                // Only update status if not currently working and not already showing a specific status like "refreshing mics"
                // Checking for parts of localized strings, e.g., "OSC服务初始化失败" (StatusOscInitFailed)
                if (!wasWorking && !_audioService.IsWorking && !StatusText.Contains(LanguageManager.GetString("StatusOscInitFailed").Split(':')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0]))
                {
                    StatusText = LanguageManager.GetString("StatusSettingsUpdated");
                    if (!Microphones.Any() || SelectedMicrophone == null)
                    {
                        StatusText += $" {LanguageManager.GetString("StatusPleaseSelectMic")}";
                    }
                    else
                    {
                        StatusText += (_appSettings.EnableOsc && _oscService != null) ? $" {LanguageManager.GetString("StatusReadyWithOsc")}" : $" {LanguageManager.GetString("StatusReadyWithoutOsc")}";
                    }
                }
                ToggleWorkCommand.NotifyCanExecuteChanged();
                RefreshMicrophonesCommand.NotifyCanExecuteChanged();
            });
        }

        private void LoadSettingsAndInitializeServices(bool reattachAudioEvents = false)
        {
            bool wasWorking = _audioService?.IsWorking ?? false;
            int? previouslySelectedMicDeviceNumber = wasWorking ? SelectedMicrophone?.WaveInDeviceIndex : null;

            _appSettings = _settingsService.LoadSettings();

            LoadTargetLanguagesFromSettings(_appSettings);

            if (reattachAudioEvents && _audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
            }
            _translationService?.Dispose();
            _audioService?.Dispose(); 
            _oscService?.Dispose(); 

            _translationService = new TranslationService(_appSettings.ServerUrl);
            _audioService = new AudioService(_appSettings);

            _audioService.AudioSegmentReady += OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated += OnAudioServiceStatusUpdate;

            if (_appSettings.EnableOsc)
            {
                try
                {
                    _oscService = new OscService(_appSettings.OscIpAddress, _appSettings.OscPort);
                    if(!wasWorking && !StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusSettingsUpdated").Split(':')[0]))
                        StatusText = string.Format(LanguageManager.GetString("StatusOscEnabled"), _appSettings.OscIpAddress, _appSettings.OscPort);
                }
                catch (Exception ex)
                {
                    _oscService = null;
                    StatusText = string.Format(LanguageManager.GetString("StatusOscInitFailed"), ex.Message);
                    AddLogMessage(string.Format(LanguageManager.GetString("LogOscInitFailed"), ex.Message));
                    System.Diagnostics.Debug.WriteLine($"OSC Service Init Error: {ex.Message}");
                }
            }
            else
            {
                _oscService = null;
            }

            // Restore working state if it was working before settings change
            if (wasWorking && previouslySelectedMicDeviceNumber.HasValue && SelectedMicrophone?.WaveInDeviceIndex == previouslySelectedMicDeviceNumber)
            {
                if (_audioService.Start(previouslySelectedMicDeviceNumber.Value))
                {
                    WorkButtonContent = LanguageManager.GetString("StopWork");
                    IsMicrophoneComboBoxEnabled = false;
                }
                else 
                {
                    WorkButtonContent = LanguageManager.GetString("StartWork"); // If start failed, revert button text
                    IsMicrophoneComboBoxEnabled = true;
                }
            } else if (wasWorking) // If settings changed while working and mic changed or wasn't previously selected
            {
                 WorkButtonContent = LanguageManager.GetString("StartWork");
                 IsMicrophoneComboBoxEnabled = true;
            }

            RefreshMicrophonesCommand.NotifyCanExecuteChanged();
            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRefreshMicrophones))]
        private async Task RefreshMicrophonesAsync()
        {
            IsRefreshingMicrophones = true;
            StatusText = LanguageManager.GetString("StatusRefreshingMics");
            IsMicrophoneComboBoxEnabled = false;

            List<MMDeviceWrapper> mics = new List<MMDeviceWrapper>();
            MMDeviceWrapper? defaultMic = null;

            try
            {
                await Task.Run(() =>
                {
                    mics = _microphoneManager.GetAvailableMicrophones(out defaultMic);
                });

                Microphones.Clear();
                foreach (var mic in mics) Microphones.Add(mic);

                if (Microphones.Any()) SelectedMicrophone = defaultMic ?? Microphones.First();
                else SelectedMicrophone = null;
            }
            catch (Exception ex)
            {
                StatusText = string.Format(LanguageManager.GetString("StatusRefreshMicsFailed"), ex.Message);
                AddLogMessage(string.Format(LanguageManager.GetString("LogRefreshMicsFailed"), ex.Message));
                System.Diagnostics.Debug.WriteLine($"RefreshMicrophones Error: {ex.ToString()}");
            }
            finally
            {
                if (SelectedMicrophone == null && !Microphones.Any()) StatusText = LanguageManager.GetString("StatusNoMicFound");
                // Only change status if it was specifically "refreshing mics" to avoid overwriting other important messages
                else if (StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0])) StatusText = LanguageManager.GetString("StatusMicsRefreshed");

                IsMicrophoneComboBoxEnabled = Microphones.Any();
                IsRefreshingMicrophones = false;
                ToggleWorkCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanExecuteRefreshMicrophones() => !_audioService.IsWorking && !IsRefreshingMicrophones;

        partial void OnSelectedMicrophoneChanged(MMDeviceWrapper? oldValue, MMDeviceWrapper? newValue)
        {
             if (newValue != null)
            {
                if (newValue.WaveInDeviceIndex != -1 && newValue.WaveInDeviceIndex < WaveIn.DeviceCount)
                {
                    // Avoid overwriting status if it's already showing something more critical like OSC init failure or refreshing status
                    if (!StatusText.Contains(LanguageManager.GetString("StatusOscEnabled").Split('(')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusSettingsUpdated").Split(':')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusRefreshingMics").Split(':')[0]) && !_audioService.IsWorking)
                        StatusText = string.Format(LanguageManager.GetString("StatusMicSelected"), newValue.FriendlyName);
                }
                else
                {
                    int cbIndex = Microphones.IndexOf(newValue);
                    if (cbIndex >= 0 && cbIndex < WaveIn.DeviceCount) {
                        newValue.WaveInDeviceIndex = cbIndex;
                         StatusText = string.Format(LanguageManager.GetString("StatusMicSelectedFallback"), newValue.FriendlyName);
                    } else {
                        StatusText = string.Format(LanguageManager.GetString("StatusMicInvalid"), newValue.FriendlyName);
                        SelectedMicrophone = null;
                    }
                }
            } else if (!Microphones.Any()){
                 StatusText = LanguageManager.GetString("StatusNoMicFoundRefreshCheck");
            }
            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsRefreshingMicrophonesChanged(bool oldValue, bool newValue)
        {
            RefreshMicrophonesCommand.NotifyCanExecuteChanged();
            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteToggleWork))]
        private async Task ToggleWorkAsync()
        {
            if (!_audioService.IsWorking)
            {
                if (SelectedMicrophone?.WaveInDeviceIndex != -1)
                {
                    bool success = false;
                    await Task.Run(() => success = _audioService.Start(SelectedMicrophone.WaveInDeviceIndex));

                    if (success)
                    {
                        WorkButtonContent = LanguageManager.GetString("StopWork");
                        IsMicrophoneComboBoxEnabled = false;
                    }
                }
                else
                {
                    MessageBox.Show(LanguageManager.GetString("MsgBoxSelectValidMicContent"), LanguageManager.GetString("MsgBoxSelectValidMicTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                await Task.Run(() => _audioService.Stop()); 
                WorkButtonContent = LanguageManager.GetString("StartWork");
                StatusText = LanguageManager.GetString("StatusStopped");
                IsMicrophoneComboBoxEnabled = true;
            }

            RefreshMicrophonesCommand.NotifyCanExecuteChanged();
            ToggleWorkCommand.NotifyCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => SelectedMicrophone != null && SelectedMicrophone.WaveInDeviceIndex != -1 && !IsRefreshingMicrophones;

        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => {
                // AudioService's 'status' is already localized, just append it to the main status text if desired.
                // Or simply set StatusText directly to the localized status from AudioService.
                StatusText = LanguageManager.GetString("StatusListening").Split(':')[0] + ": " + status; // Example: "状态: 正在监听..."
                AddLogMessage($"AudioService Status: {status}"); // Keep raw status in detailed logs
            });
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            string currentUiStatus = string.Format(LanguageManager.GetString("StatusSendingSegment"), e.TriggerReason);
            Application.Current.Dispatcher.Invoke(() => StatusText = currentUiStatus);
            AddLogMessage(string.Format(LanguageManager.GetString("LogSendingSegment"), e.TriggerReason, e.AudioData.Length, DateTime.Now));


            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            var (response, rawJsonResponse, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty;
            string logEntry;

            if (!string.IsNullOrEmpty(rawJsonResponse))
            {
                AddLogMessage(string.Format(LanguageManager.GetString("LogServerRawResponse"), e.TriggerReason, rawJsonResponse));
            }

            if (errorMessage != null)
            {
                currentUiStatus = LanguageManager.GetString("StatusTranslationFailed");
                TranslationResultText = string.Format(LanguageManager.GetString("TranslationError"), errorMessage);
                logEntry = string.Format(LanguageManager.GetString("LogTranslationError"), e.TriggerReason, errorMessage);
            }
            else if (response != null)
            {
                if (response.Status == "success" && response.Data != null && !string.IsNullOrEmpty(response.Data.Raw_Text))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccess");
                    TranslationResultText = response.Data.Raw_Text;
                    translatedTextForOsc = response.Data.Raw_Text;
                    logEntry = string.Format(LanguageManager.GetString("LogTranslationSuccess"), e.TriggerReason, response.Data.Raw_Text, response.Duration_Seconds);
                }
                else if (response.Status == "success" && (response.Data == null || string.IsNullOrEmpty(response.Data.Raw_Text)))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccessNoText");
                    TranslationResultText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    logEntry = string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"), e.TriggerReason, response.Duration_Seconds);
                }
                else
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationFailedServer");
                    TranslationResultText = string.Format(LanguageManager.GetString("TranslationServerError"), response.Message ?? LanguageManager.GetString("UnknownError"));
                    logEntry = string.Format(LanguageManager.GetString("LogServerError"), e.TriggerReason, response.Message ?? LanguageManager.GetString("UnknownError"));
                    if(response.Details != null) logEntry += string.Format(LanguageManager.GetString("LogServerErrorDetails"), response.Details.Content ?? "N/A");
                }
            }
            else
            {
                currentUiStatus = LanguageManager.GetString("StatusEmptyResponse");
                TranslationResultText = LanguageManager.GetString("TranslationEmptyResponseError");
                logEntry = string.Format(LanguageManager.GetString("LogEmptyResponse"), e.TriggerReason);
            }
            
            Application.Current.Dispatcher.Invoke(() => {
                StatusText = currentUiStatus;
                AddLogMessage(logEntry);
            });


            if (_appSettings.EnableOsc && _oscService != null && !string.IsNullOrEmpty(translatedTextForOsc))
            {
                Application.Current.Dispatcher.Invoke(() => StatusText = LanguageManager.GetString("StatusSendingToVRChat"));
                try
                {
                    await _oscService.SendChatboxMessageAsync(
                        translatedTextForOsc, 
                        _appSettings.OscSendImmediately, 
                        _appSettings.OscPlayNotificationSound
                    );
                    Application.Current.Dispatcher.Invoke(() => {
                        // Combine two localized strings
                        StatusText = string.Format(LanguageManager.GetString("StatusTranslationSuccess") + " " + LanguageManager.GetString("LogOscSent").Replace("[OSC] ", ""), translatedTextForOsc.Split('\n')[0]);
                        AddLogMessage(string.Format(LanguageManager.GetString("LogOscSent"), translatedTextForOsc.Split('\n')[0]));
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OSC Send Error: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() => {
                        string oscErrorMsg = string.Format(LanguageManager.GetString("StatusOscSendFailed"), ex.Message.Split('\n')[0]);
                        StatusText = oscErrorMsg;
                        AddLogMessage(string.Format(LanguageManager.GetString("LogOscSendFailed"), ex.Message));
                    });
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Revert to "Listening..." status only if AudioService is still active and no other critical message is displayed
                if (_audioService.IsWorking && !StatusText.Contains(LanguageManager.GetString("AudioStatusSpeechDetected").Split('.')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusTranslationFailed").Split(':')[0]) && !StatusText.Contains(LanguageManager.GetString("StatusOscSendFailed").Split(':')[0]))
                {
                    StatusText = LanguageManager.GetString("StatusListening");
                }
            });
        }

        // --- Target Language Management ---
        private void LoadTargetLanguagesFromSettings(AppSettings settings)
        {
            TargetLanguageItems.Clear();
            var languagesFromSettings = string.IsNullOrWhiteSpace(settings.TargetLanguages)
                ? new List<string>()
                : settings.TargetLanguages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim())
                                         .Where(s => AllSupportedLanguages.Contains(s)) 
                                         .Distinct() 
                                         .ToList();

            if (!languagesFromSettings.Any())
            {
                languagesFromSettings.Add(AllSupportedLanguages.FirstOrDefault() ?? LanguageManager.GetString("DefaultEnglishLanguageName"));
            }

            foreach (var lang in languagesFromSettings.Take(MaxTargetLanguages)) 
            {
                var newItem = new SelectableTargetLanguageViewModel(this, lang, new List<string>(AllSupportedLanguages));
                TargetLanguageItems.Add(newItem);
            }
            UpdateItemPropertiesAndAvailableLanguages();
            AddLanguageCommand.NotifyCanExecuteChanged();
        }
        
        [RelayCommand(CanExecute = nameof(CanExecuteAddLanguage))]
        private void AddLanguage()
        {
            if (!CanExecuteAddLanguage()) return;
            string defaultNewLang = AllSupportedLanguages.FirstOrDefault(l => !TargetLanguageItems.Any(item => item.SelectedLanguage == l))
                                    ?? AllSupportedLanguages.First(); 
            var newItem = new SelectableTargetLanguageViewModel(this, defaultNewLang, new List<string>(AllSupportedLanguages));
            TargetLanguageItems.Add(newItem);
            UpdateItemPropertiesAndAvailableLanguages();
            AddLanguageCommand.NotifyCanExecuteChanged();
            SaveCurrentSettings();
        }

        private bool CanExecuteAddLanguage()
        {
            return TargetLanguageItems.Count < MaxTargetLanguages;
        }

        public void RemoveLanguageItem(SelectableTargetLanguageViewModel itemToRemove)
        {
            if (TargetLanguageItems.Contains(itemToRemove))
            {
                TargetLanguageItems.Remove(itemToRemove);
                UpdateItemPropertiesAndAvailableLanguages();
                AddLanguageCommand.NotifyCanExecuteChanged();
                SaveCurrentSettings();
            }
        }

        public void OnLanguageSelectionChanged(SelectableTargetLanguageViewModel changedItem)
        {
            UpdateItemPropertiesAndAvailableLanguages();
            SaveCurrentSettings();
        }

        private void UpdateItemPropertiesAndAvailableLanguages()
        {
            for (int i = 0; i < TargetLanguageItems.Count; i++)
            {
                var itemVm = TargetLanguageItems[i];
                itemVm.Label = $"目标 {i + 1}:"; // This string could also be localized if "目标" is dynamic, but for now it's fine.
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

        private void SaveCurrentSettings()
        {
            var selectedLangsList = TargetLanguageItems
                .Select(item => item.SelectedLanguage)
                .Where(lang => !string.IsNullOrWhiteSpace(lang) && AllSupportedLanguages.Contains(lang))
                .Distinct()
                .ToList();
            _appSettings.TargetLanguages = string.Join(",", selectedLangsList);

            _settingsService.SaveSettings(_appSettings);
            SettingsChangedNotifier.RaiseSettingsChanged();
            AddLogMessage(LanguageManager.GetString("LogTargetLangsSaved"));
        }

        // --- Log Management ---
        private void AddLogMessage(string message)
        {
            Debug.WriteLine($"AddLogMessage INVOKED with: \"{message}\". Current LogMessages count before add: {LogMessages.Count}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                LogMessages.Add(timestampedMessage);
                Debug.WriteLine($"LogMessages UPDATED. New count: {LogMessages.Count}. Last message: \"{LogMessages.LastOrDefault()}\"");
                while (LogMessages.Count > MaxLogEntries)
                {
                    LogMessages.RemoveAt(0);
                }
            });
        }

        [RelayCommand]
        private void ClearLog()
        {
            Debug.WriteLine($"ExecuteClearLog INVOKED. LogMessages count BEFORE clear: {LogMessages.Count}");
            LogMessages.Clear();
            Debug.WriteLine($"ExecuteClearLog: LogMessages.Clear() called. LogMessages count AFTER clear: {LogMessages.Count}");
            AddLogMessage(LanguageManager.GetString("LogCleared"));
        }

        public void Dispose()
        {
            SettingsChangedNotifier.SettingsChanged -= OnGlobalSettingsChanged;
            if (LogMessages != null)
            {
                LogMessages.CollectionChanged -= OnLogMessagesChanged;
            }
            if (_audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
                _audioService.Dispose();
            }
            _translationService?.Dispose();
            _oscService?.Dispose(); 
            GC.SuppressFinalize(this);
        }
    }
}