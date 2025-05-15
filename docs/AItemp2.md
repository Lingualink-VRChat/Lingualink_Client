Okay, this is a significant set of changes, but it will make the UI more aligned with your workflow. Let's break this down and implement it.

**Summary of Changes:**

1.  **Target Languages on `IndexPage`**:
    *   Move the "Target Languages" UI and logic from `ServicePage` / `ServicePageViewModel` to `IndexPage` / `IndexWindowViewModel`.
    *   Changes to target languages on `IndexPage` will be saved immediately.
2.  **OSC Expander on `ServicePage`**:
    *   Decouple the OSC settings expander's `IsExpanded` state from `EnableOsc`. The expander can be opened/closed independently.
    *   The `CheckBox` inside the expander will control `EnableOsc`.
3.  **`IndexPage` Translation Result Display**:
    *   Show only the current translation (overwrite previous).
    *   Remove LLM processing time from this display.
4.  **New Log View on `IndexPage`**:
    *   Add a new column for a log display.
    *   Log will show server responses (raw text, errors, messages).
    *   Add a "Clear Log" button.
    *   Log limited to 500 entries.

Here are the modified files:

**1. `ViewModels/SelectableTargetLanguageViewModel.cs`**
   *   Change `ParentViewModel` type to `IndexWindowViewModel`.

```csharp
// File: ViewModels/SelectableTargetLanguageViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>

namespace lingualink_client.ViewModels
{
    public class SelectableTargetLanguageViewModel : ViewModelBase
    {
        // Changed ParentViewModel type
        public IndexWindowViewModel ParentViewModel { get; }

        private string _selectedLanguage;
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    ParentViewModel?.OnLanguageSelectionChanged(this);
                }
            }
        }

        private ObservableCollection<string> _availableLanguages;
        public ObservableCollection<string> AvailableLanguages
        {
            get => _availableLanguages;
            set => SetProperty(ref _availableLanguages, value);
        }

        private string _label;
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private bool _canRemove;
        public bool CanRemove
        {
            get => _canRemove;
            set
            {
                if (SetProperty(ref _canRemove, value))
                {
                    RemoveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RemoveCommand { get; }

        // Changed constructor parameter type
        public SelectableTargetLanguageViewModel(IndexWindowViewModel parent, string initialSelection, List<string> allLangsSeed)
        {
            ParentViewModel = parent;
            _availableLanguages = new ObservableCollection<string>(allLangsSeed);
            _selectedLanguage = initialSelection;
            
            RemoveCommand = new DelegateCommand(
                _ => ParentViewModel.RemoveLanguageItem(this),
                _ => CanRemove
            );
        }
    }
}
```

**2. `ViewModels/IndexWindowViewModel.cs`**
   *   Incorporate target language management.
   *   Add logging capabilities.
   *   Modify translation result display logic.
   *   Implement immediate save for target language changes.

```csharp
// File: ViewModels/IndexWindowViewModel.cs
using NAudio.Wave; // For WaveFormat
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class IndexWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly MicrophoneManager _microphoneManager;
        private AudioService _audioService;
        private TranslationService _translationService;
        private readonly SettingsService _settingsService;
        private OscService? _oscService;
        private AppSettings _appSettings;
        
        // Target Language Properties (moved from ServicePageViewModel)
        public ObservableCollection<SelectableTargetLanguageViewModel> TargetLanguageItems { get; }
        public DelegateCommand AddLanguageCommand { get; }
        private static readonly List<string> AllSupportedLanguages = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
        };
        private const int MaxTargetLanguages = 5;

        // Log Properties
        public ObservableCollection<string> LogMessages { get; }
        public DelegateCommand ClearLogCommand { get; }
        private const int MaxLogEntries = 500;

        private ObservableCollection<MMDeviceWrapper> _microphones = new ObservableCollection<MMDeviceWrapper>();
        public ObservableCollection<MMDeviceWrapper> Microphones
        {
            get => _microphones;
            set => SetProperty(ref _microphones, value);
        }

        private MMDeviceWrapper? _selectedMicrophone;
        public MMDeviceWrapper? SelectedMicrophone
        {
            get => _selectedMicrophone;
            set
            {
                if (SetProperty(ref _selectedMicrophone, value))
                {
                    OnSelectedMicrophoneChanged();
                    ToggleWorkCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusText = "状态：初始化...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _translationResultText = string.Empty;
        public string TranslationResultText
        {
            get => _translationResultText;
            set => SetProperty(ref _translationResultText, value);
        }

        private string _workButtonContent = "开始工作";
        public string WorkButtonContent
        {
            get => _workButtonContent;
            set => SetProperty(ref _workButtonContent, value);
        }

        private bool _isMicrophoneComboBoxEnabled = true;
        public bool IsMicrophoneComboBoxEnabled
        {
            get => _isMicrophoneComboBoxEnabled;
            set => SetProperty(ref _isMicrophoneComboBoxEnabled, value);
        }

        private bool _isRefreshingMicrophones = false;
        public bool IsRefreshingMicrophones 
        {
            get => _isRefreshingMicrophones;
            set
            {
                if (SetProperty(ref _isRefreshingMicrophones, value))
                {
                    RefreshMicrophonesCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RefreshMicrophonesCommand { get; }
        public DelegateCommand ToggleWorkCommand { get; }

        public IndexWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService();
            
            TargetLanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();
            AddLanguageCommand = new DelegateCommand(ExecuteAddLanguage, CanExecuteAddLanguage);

            LogMessages = new ObservableCollection<string>();
            ClearLogCommand = new DelegateCommand(ExecuteClearLog);
            
            RefreshMicrophonesCommand = new DelegateCommand(async _ => await ExecuteRefreshMicrophonesAsync(), _ => CanExecuteRefreshMicrophones());
            ToggleWorkCommand = new DelegateCommand(async _ => await ExecuteToggleWorkAsync(), _ => CanExecuteToggleWork());

            LoadSettingsAndInitializeServices(); 
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged;

            _ = ExecuteRefreshMicrophonesAsync(); 
        }
        
        private void OnGlobalSettingsChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool wasWorking = _audioService?.IsWorking ?? false;
                LoadSettingsAndInitializeServices(true); // This will reload _appSettings and re-init services

                // Smart status update logic (simplified, as LoadSettingsAndInitializeServices handles OSC status)
                if (!wasWorking && !_audioService.IsWorking && !StatusText.Contains("OSC服务初始化失败") && !StatusText.Contains("正在刷新麦克风列表..."))
                {
                    StatusText = "状态：设置已更新。";
                    if (!Microphones.Any() || SelectedMicrophone == null)
                    {
                        StatusText += " 请选择麦克风。";
                    }
                    else
                    {
                        StatusText += (_appSettings.EnableOsc && _oscService != null) ? " 可开始工作并发送至VRChat。" : " 可开始工作。";
                    }
                }
                ToggleWorkCommand.RaiseCanExecuteChanged();
                RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            });
        }

        private void LoadSettingsAndInitializeServices(bool reattachAudioEvents = false)
        {
            bool wasWorking = _audioService?.IsWorking ?? false;
            int? previouslySelectedMicDeviceNumber = wasWorking ? SelectedMicrophone?.WaveInDeviceIndex : null;

            _appSettings = _settingsService.LoadSettings(); // Load latest settings

            // Load target languages into UI
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
            _audioService = new AudioService(_appSettings); // AudioService uses VAD params from _appSettings

            _audioService.AudioSegmentReady += OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated += OnAudioServiceStatusUpdate;

            if (_appSettings.EnableOsc)
            {
                try
                {
                    _oscService = new OscService(_appSettings.OscIpAddress, _appSettings.OscPort);
                     if(!wasWorking && !StatusText.Contains("正在刷新麦克风列表...") && !StatusText.Contains("设置已更新"))
                        StatusText = $"状态：OSC服务已启用 ({_appSettings.OscIpAddress}:{_appSettings.OscPort})";
                }
                catch (Exception ex)
                {
                    _oscService = null;
                    StatusText = $"状态：OSC服务初始化失败: {ex.Message}";
                    AddLogMessage($"OSC服务初始化失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"OSC Service Init Error: {ex.Message}");
                }
            }
            else
            {
                _oscService = null;
            }

            if (wasWorking && previouslySelectedMicDeviceNumber.HasValue && SelectedMicrophone?.WaveInDeviceIndex == previouslySelectedMicDeviceNumber)
            {
                if (_audioService.Start(previouslySelectedMicDeviceNumber.Value))
                {
                    WorkButtonContent = "停止工作";
                    IsMicrophoneComboBoxEnabled = false;
                }
                else 
                {
                    WorkButtonContent = "开始工作";
                    IsMicrophoneComboBoxEnabled = true;
                }
            } else if (wasWorking) 
            {
                 WorkButtonContent = "开始工作";
                 IsMicrophoneComboBoxEnabled = true;
            }

            RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            ToggleWorkCommand.RaiseCanExecuteChanged();
        }

        private async Task ExecuteRefreshMicrophonesAsync()
        {
            // ... (method remains the same)
            IsRefreshingMicrophones = true;
            StatusText = "状态：正在刷新麦克风列表...";
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
                
                OnSelectedMicrophoneChanged(); 
            }
            catch (Exception ex)
            {
                StatusText = $"状态：刷新麦克风列表失败: {ex.Message}";
                 AddLogMessage($"刷新麦克风列表失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RefreshMicrophones Error: {ex.ToString()}");
            }
            finally
            {
                if (SelectedMicrophone == null && !Microphones.Any()) StatusText = "状态：未找到可用的麦克风设备！";
                else if (StatusText.Contains("正在刷新麦克风列表...")) StatusText = "状态：麦克风列表已刷新。";

                IsMicrophoneComboBoxEnabled = Microphones.Any();
                IsRefreshingMicrophones = false;
                ToggleWorkCommand.RaiseCanExecuteChanged(); 
            }
        }

        private bool CanExecuteRefreshMicrophones() => !_audioService.IsWorking && !IsRefreshingMicrophones;

        private void OnSelectedMicrophoneChanged()
        {
            // ... (method remains largely the same, status updates might be more nuanced)
             if (_selectedMicrophone != null)
            {
                if (_selectedMicrophone.WaveInDeviceIndex != -1 && _selectedMicrophone.WaveInDeviceIndex < WaveIn.DeviceCount)
                {
                     // Avoid overriding more important status messages
                     if (!StatusText.Contains("OSC服务") && !StatusText.Contains("设置已更新") && !StatusText.Contains("正在刷新") && !_audioService.IsWorking)
                        StatusText = $"状态：已选择麦克风: {_selectedMicrophone.FriendlyName}";
                }
                else
                {
                    int cbIndex = Microphones.IndexOf(_selectedMicrophone);
                    if (cbIndex >= 0 && cbIndex < WaveIn.DeviceCount) {
                        _selectedMicrophone.WaveInDeviceIndex = cbIndex;
                         StatusText = $"状态：已选择麦克风 (回退索引): {_selectedMicrophone.FriendlyName}";
                    } else {
                        StatusText = $"状态：麦克风 '{_selectedMicrophone.FriendlyName}' 无效。";
                        _selectedMicrophone = null; 
                    }
                }
            } else if (!Microphones.Any()){
                 StatusText = "状态：未找到可用的麦克风设备。请刷新或检查设备。";
            }
            ToggleWorkCommand.RaiseCanExecuteChanged();
        }

        private async Task ExecuteToggleWorkAsync()
        {
            // ... (method remains the same)
            if (!_audioService.IsWorking)
            {
                if (SelectedMicrophone?.WaveInDeviceIndex != -1)
                {
                    bool success = false;
                    await Task.Run(() => success = _audioService.Start(SelectedMicrophone.WaveInDeviceIndex));

                    if (success)
                    {
                        WorkButtonContent = "停止工作";
                        IsMicrophoneComboBoxEnabled = false;
                    }
                }
                else
                {
                    MessageBox.Show("请选择一个有效的麦克风设备。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                await Task.Run(() => _audioService.Stop()); 
                WorkButtonContent = "开始工作";
                StatusText = "状态：已停止。";
                IsMicrophoneComboBoxEnabled = true;
            }

            RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            ToggleWorkCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => SelectedMicrophone != null && SelectedMicrophone.WaveInDeviceIndex != -1 && !IsRefreshingMicrophones;

        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => {
                StatusText = $"状态：{status}";
                // Optionally add to log, but might be too noisy
                // AddLogMessage($"AudioService: {status}");
            });
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            string currentUiStatus = $"状态：正在发送片段 ({e.TriggerReason})...";
            Application.Current.Dispatcher.Invoke(() => StatusText = currentUiStatus);
            AddLogMessage($"发送片段 ({e.TriggerReason}, {e.AudioData.Length} bytes) at {DateTime.Now:HH:mm:ss}");


            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            var (response, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty;
            string logEntry;

            if (errorMessage != null)
            {
                currentUiStatus = "状态：翻译请求失败。";
                TranslationResultText = $"错误: {errorMessage}";
                logEntry = $"翻译错误 ({e.TriggerReason}): {errorMessage}";
            }
            else if (response != null)
            {
                if (response.Status == "success" && response.Data != null && !string.IsNullOrEmpty(response.Data.Raw_Text))
                {
                    currentUiStatus = "状态：翻译成功！";
                    TranslationResultText = response.Data.Raw_Text; // Overwrite, no duration
                    translatedTextForOsc = response.Data.Raw_Text;
                    logEntry = $"翻译成功 ({e.TriggerReason}): \"{response.Data.Raw_Text}\" (LLM: {response.Duration_Seconds:F2}s)";
                }
                else if (response.Status == "success" && (response.Data == null || string.IsNullOrEmpty(response.Data.Raw_Text)))
                {
                    currentUiStatus = "状态：翻译成功，但无文本内容。";
                    TranslationResultText = "(服务器返回成功，但无文本内容)";
                    logEntry = $"翻译成功但无文本 ({e.TriggerReason}). (LLM: {response.Duration_Seconds:F2}s)";
                }
                else
                {
                    currentUiStatus = "状态：翻译失败 (服务器)。";
                    TranslationResultText = $"服务器错误: {response.Message ?? "未知错误"}";
                    logEntry = $"服务器处理错误 ({e.TriggerReason}): {response.Message ?? "未知错误"}";
                    if(response.Details != null) logEntry += $" | 详情: {response.Details.Content ?? "N/A"}";
                }
            }
            else
            {
                currentUiStatus = "状态：收到空响应。";
                TranslationResultText = "错误: 服务器空响应";
                logEntry = $"收到空响应 ({e.TriggerReason})";
            }
            
            Application.Current.Dispatcher.Invoke(() => {
                StatusText = currentUiStatus;
                AddLogMessage(logEntry);
            });


            if (_appSettings.EnableOsc && _oscService != null && !string.IsNullOrEmpty(translatedTextForOsc))
            {
                Application.Current.Dispatcher.Invoke(() => StatusText = "状态：翻译成功！正在发送到VRChat...");
                try
                {
                    await _oscService.SendChatboxMessageAsync(
                        translatedTextForOsc, 
                        _appSettings.OscSendImmediately, 
                        _appSettings.OscPlayNotificationSound
                    );
                    Application.Current.Dispatcher.Invoke(() => {
                        StatusText = "状态：翻译成功！已发送到VRChat。";
                        AddLogMessage($"[OSC] 消息已发送到VRChat: \"{translatedTextForOsc.Split('\n')[0]}...\"");
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OSC Send Error: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() => {
                        string oscErrorMsg = $"状态：翻译成功！但VRChat发送失败: {ex.Message.Split('\n')[0]}";
                        StatusText = oscErrorMsg;
                        AddLogMessage($"[OSC ERROR] 发送失败: {ex.Message}");
                    });
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_audioService.IsWorking && !StatusText.Contains("检测到语音") && !StatusText.Contains("失败") && !StatusText.Contains("VRChat"))
                {
                    StatusText = "状态：正在监听...";
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
            SaveCurrentSettings(); // Save when target languages change
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
                SaveCurrentSettings(); // Save when target languages change
            }
        }

        public void OnLanguageSelectionChanged(SelectableTargetLanguageViewModel changedItem)
        {
            UpdateItemPropertiesAndAvailableLanguages();
            SaveCurrentSettings(); // Save when target languages change
        }

        private void UpdateItemPropertiesAndAvailableLanguages()
        {
            for (int i = 0; i < TargetLanguageItems.Count; i++)
            {
                var itemVm = TargetLanguageItems[i];
                itemVm.Label = $"目标 {i + 1}:"; // Shortened Label
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
            // Update _appSettings with current target languages
            var selectedLangsList = TargetLanguageItems
                .Select(item => item.SelectedLanguage)
                .Where(lang => !string.IsNullOrWhiteSpace(lang) && AllSupportedLanguages.Contains(lang))
                .Distinct()
                .ToList();
            _appSettings.TargetLanguages = string.Join(",", selectedLangsList);

            // Save all current settings (including those from ServicePage that are in _appSettings)
            _settingsService.SaveSettings(_appSettings);
            SettingsChangedNotifier.RaiseSettingsChanged(); // Notify other parts of the app
            AddLogMessage("目标语言设置已更新并保存。");
        }

        // --- Log Management ---
        private void AddLogMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                LogMessages.Add(timestampedMessage);
                while (LogMessages.Count > MaxLogEntries)
                {
                    LogMessages.RemoveAt(0);
                }
            });
        }

        private void ExecuteClearLog(object? parameter)
        {
            LogMessages.Clear();
            AddLogMessage("日志已清除。");
        }

        public void Dispose()
        {
            SettingsChangedNotifier.SettingsChanged -= OnGlobalSettingsChanged;
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
```

**3. `ViewModels/ServicePageViewModel.cs`**
   *   Remove target language management.
   *   OSC `EnableOsc` logic remains, but expander's visual state is separate.

```csharp
// File: ViewModels/ServicePageViewModel.cs
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
```

**4. `Views/Pages/IndexPage.xaml`**
   *   Add UI for target languages.
   *   Add UI for the log.
   *   Restructure layout.

```xml
<Page
    x:Class="lingualink_client.Views.IndexPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client.Views"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    xmlns:converters="clr-namespace:lingualink_client.Converters"
    Title="IndexPage"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    mc:Ignorable="d">

    <Grid Margin="10">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" MinWidth="350" /> <!-- Main controls -->
            <ColumnDefinition Width="Auto" /> <!-- GridSplitter -->
            <ColumnDefinition Width="1*" MinWidth="250" /> <!-- Log -->
        </Grid.ColumnDefinitions>

        <!-- Left Column: Main Controls -->
        <Grid Grid.Column="0" Margin="0,0,5,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Mic Selection -->
                <RowDefinition Height="Auto" /> <!-- Target Languages Expander -->
                <RowDefinition Height="Auto" /> <!-- Work Button -->
                <RowDefinition Height="Auto" /> <!-- Status Text -->
                <RowDefinition Height="*" />   <!-- Translation Result -->
                <RowDefinition Height="Auto" /> <!-- Hint -->
            </Grid.RowDefinitions>

            <StackPanel
                Grid.Row="0"
                Margin="0,0,0,10"
                Orientation="Horizontal">
                <Label VerticalAlignment="Center" Content="选择麦克风：" />
                <ComboBox
                    Width="200"
                    Margin="5,0,0,0"
                    VerticalAlignment="Center"
                    DisplayMemberPath="FriendlyName"
                    IsEnabled="{Binding IsMicrophoneComboBoxEnabled}"
                    ItemsSource="{Binding Microphones}"
                    SelectedItem="{Binding SelectedMicrophone}" />
                <Button
                    Margin="10,0,0,0"
                    Padding="5,2"
                    VerticalAlignment="Center"
                    Command="{Binding RefreshMicrophonesCommand}"
                    Content="刷新" />
                <StatusBar
                    Margin="10,0,0,0"
                    HorizontalAlignment="Right"
                    VerticalAlignment="Center"
                    Background="Transparent"
                    Opacity="0.7"
                    Visibility="{Binding IsRefreshingMicrophones, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <StatusBarItem>
                        <TextBlock Text="正在刷新麦克风..." />
                    </StatusBarItem>
                </StatusBar>
            </StackPanel>

            <Expander Grid.Row="1" Header="目标翻译语言" Margin="0,0,0,10" IsExpanded="True">
                <StackPanel Margin="10,5,0,0">
                    <ItemsControl ItemsSource="{Binding TargetLanguageItems}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type vm:SelectableTargetLanguageViewModel}">
                                <Grid Margin="0,3,0,3">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Margin="0,0,8,0" VerticalAlignment="Center" Text="{Binding Label}" />
                                    <ComboBox Grid.Column="1" MinWidth="150" MaxWidth="200" HorizontalAlignment="Stretch"
                                              ItemsSource="{Binding AvailableLanguages}" SelectedItem="{Binding SelectedLanguage}" />
                                    <Button Grid.Column="2" Margin="8,0,0,0" Padding="5,2" VerticalAlignment="Center"
                                            Command="{Binding RemoveCommand}" Content="移除"
                                            Visibility="{Binding CanRemove, Converter={StaticResource BooleanToVisibilityConverter}}" />
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <Button Margin="0,5,0,0" Padding="5,2" HorizontalAlignment="Left"
                            Command="{Binding AddLanguageCommand}" Content="增加语言" />
                </StackPanel>
            </Expander>


            <Button
                Grid.Row="2" Margin="0,0,0,10"
                Padding="10,5"
                HorizontalAlignment="Left"
                Command="{Binding ToggleWorkCommand}"
                Content="{Binding WorkButtonContent}" />

            <TextBlock
                Grid.Row="3"
                Margin="0,0,0,10"
                FontSize="14"
                Text="{Binding StatusText}" />

            <TextBox
                Grid.Row="4"
                Margin="0,0,0,10"
                AcceptsReturn="True"
                IsReadOnly="True"
                Text="{Binding TranslationResultText, Mode=OneWay}"
                TextWrapping="Wrap"
                VerticalScrollBarVisibility="Auto" />

            <TextBlock
                Grid.Row="5"
                FontStyle="Italic"
                Foreground="Gray"
                Text="提示：点击“开始工作”后，应用将持续监听麦克风进行VAD检测。" />
        </Grid>

        <!-- GridSplitter -->
        <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Center" VerticalAlignment="Stretch" Background="LightGray" ShowsPreview="True"/>

        <!-- Right Column: Log -->
        <Grid Grid.Column="2" Margin="5,0,0,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Log Label & Clear Button -->
                <RowDefinition Height="*" />   <!-- Log TextBox -->
            </Grid.RowDefinitions>

            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,5">
                <Label Content="运行日志:" FontWeight="SemiBold"/>
                <Button Content="清除日志" Command="{Binding ClearLogCommand}" HorizontalAlignment="Right" Margin="10,0,0,0" Padding="5,2"/>
            </StackPanel>
            
            <TextBox Grid.Row="1"
                     IsReadOnly="True"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto"
                     ItemsSource="{Binding LogMessages}" >
                <TextBox.Text>
                    <MultiBinding StringFormat="{}{0}">
                        <Binding Path="LogMessages" Converter="{StaticResource StringJoinConverter}"/>
                    </MultiBinding>
                </TextBox.Text>
            </TextBox>
        </Grid>
    </Grid>
</Page>
```
To make the `LogMessages` (which is an `ObservableCollection<string>`) display correctly in the `TextBox`, you'll need a simple converter or to handle it differently. A `TextBox` binds its `Text` property to a single string.
Let's create a simple `StringJoinConverter` for the log `TextBox`.

**5. Create `Converters/StringJoinConverter.cs`**

```csharp
// File: Converters/StringJoinConverter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace lingualink_client.Converters
{
    public class StringJoinConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> lines)
            {
                return string.Join(Environment.NewLine, lines);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // Not needed for one-way display
        }
    }
}
```
And add it to `App.xaml` resources:

```xml
<!-- File: App.xaml (add to Application.Resources) -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Light" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
        <converters:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
        <converters:StringJoinConverter x:Key="StringJoinConverter" /> <!-- Add this line -->
    </ResourceDictionary>
</Application.Resources>
```

Then modify `IndexPage.xaml` for the log `TextBox`:

```xml
<!-- In Views/Pages/IndexPage.xaml, Right Column Log TextBox -->
            <TextBox Grid.Row="1"
                     IsReadOnly="True"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto"
                     Text="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}" />
```
The `MultiBinding` I initially put was an overcomplication. A direct binding with the converter is cleaner if the `ItemsSource` property on `TextBox` is not what we want (it's more for list-like controls). `TextBox.Text` expects a string.

**6. `Views/Pages/ServicePage.xaml`**
   *   Remove UI for target languages.
   *   Adjust OSC Expander's `IsExpanded` behavior.

```xml
<Page
    x:Class="lingualink_client.Views.ServicePage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client.Views"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    xmlns:converters="clr-namespace:lingualink_client.Converters" 
    Title="ServicePage"
    d:DataContext="{d:DesignInstance Type=vm:ServicePageViewModel}"
    d:DesignHeight="600"
    d:DesignWidth="800"
    mc:Ignorable="d">
    <Page.Resources>
        <!-- BooleanToVisibilityConverter is in App.xaml -->
    </Page.Resources>
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <Grid Margin="15">
            <Grid.RowDefinitions>
                <!-- Row 0-3 were for Target Languages, now removed -->
                <RowDefinition Height="Auto" /> <!-- Server URL Label -->
                <RowDefinition Height="Auto" /> <!-- Server URL TextBox -->
                <RowDefinition Height="Auto" /> <!-- VAD Expander -->
                <RowDefinition Height="Auto" /> <!-- OSC Expander -->
                <RowDefinition Height="*" />   <!-- Spacer -->
                <RowDefinition Height="Auto" /> <!-- Save/Cancel Buttons -->
            </Grid.RowDefinitions>

            <!-- Target Language UI Removed -->

            <Label Grid.Row="0" Content="服务器 URL:" />
            <TextBox
                x:Name="ServerUrlTextBox"
                Grid.Row="1"
                Margin="0,0,0,10"
                Text="{Binding ServerUrl, UpdateSourceTrigger=PropertyChanged}" />

            <Expander
                Grid.Row="2"
                Margin="0,0,0,10"
                Header="高级VAD设置"
                IsExpanded="False"> <!-- Default to not expanded -->
                <StackPanel Margin="10,5,0,0">
                    <Label Content="静音检测阈值 (秒):" />
                    <TextBox Margin="0,0,0,5" Text="{Binding SilenceThresholdSeconds, UpdateSourceTrigger=PropertyChanged, StringFormat='N2'}" />
                    <Label Content="最小语音时长 (秒):" />
                    <TextBox Margin="0,0,0,5" Text="{Binding MinVoiceDurationSeconds, UpdateSourceTrigger=PropertyChanged, StringFormat='N2'}" />
                    <Label Content="最大语音时长 (秒):" />
                    <TextBox Margin="0,0,0,5" Text="{Binding MaxVoiceDurationSeconds, UpdateSourceTrigger=PropertyChanged, StringFormat='N2'}" />
                    <Label Margin="0,5,0,0" Content="最小录音音量阈值 (0-100%):" />
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <Slider
                            Grid.Column="0"
                            Margin="0,0,5,5"
                            VerticalAlignment="Center"
                            LargeChange="0.1"
                            Maximum="1.0"
                            Minimum="0"
                            SmallChange="0.01"
                            TickFrequency="0.05"
                            Value="{Binding MinRecordingVolumeThreshold, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox
                            Grid.Column="1"
                            Width="50"
                            Margin="0,0,0,5"
                            VerticalAlignment="Center"
                            Text="{Binding MinRecordingVolumeThreshold, StringFormat='P0', UpdateSourceTrigger=PropertyChanged}"
                            TextAlignment="Right" />
                    </Grid>
                    <TextBlock
                        FontSize="10"
                        FontStyle="Italic"
                        Foreground="Gray"
                        Text="提示: 仅当麦克风输入音量高于此阈值时才开始处理语音。0% 表示禁用此过滤。"
                        TextWrapping="Wrap" />
                </StackPanel>
            </Expander>

            <Expander
                Grid.Row="3"
                Margin="0,0,0,10"
                Header="VRChat OSC 发送设置"
                IsExpanded="False"> <!-- Default to not expanded, not bound to EnableOsc -->
                <StackPanel Margin="10,5,0,0">
                    <CheckBox
                        Margin="0,0,0,5"
                        Content="启用 OSC 发送 (勾选后下方设置生效)"
                        IsChecked="{Binding EnableOsc}" /> <!-- This controls the actual OSC enabling -->
                    <Grid IsEnabled="{Binding EnableOsc}"> <!-- This grid is enabled/disabled based on the checkbox -->
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="Auto" />
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <Label
                            Grid.Row="0"
                            Grid.Column="0"
                            VerticalAlignment="Center"
                            Content="OSC IP 地址:" />
                        <TextBox
                            Grid.Row="0"
                            Grid.Column="1"
                            Margin="0,0,0,5"
                            VerticalAlignment="Center"
                            Text="{Binding OscIpAddress, UpdateSourceTrigger=PropertyChanged}" />
                        <Label
                            Grid.Row="1"
                            Grid.Column="0"
                            VerticalAlignment="Center"
                            Content="OSC 端口:" />
                        <TextBox
                            Grid.Row="1"
                            Grid.Column="1"
                            Margin="0,0,0,5"
                            VerticalAlignment="Center"
                            Text="{Binding OscPort, UpdateSourceTrigger=PropertyChanged}" />
                        <CheckBox
                            Grid.Row="2"
                            Grid.Column="0"
                            Grid.ColumnSpan="2"
                            Margin="0,5,0,5"
                            Content="立即发送 (绕过键盘)"
                            IsChecked="{Binding OscSendImmediately}" />
                        <CheckBox
                            Grid.Row="3"
                            Grid.Column="0"
                            Grid.ColumnSpan="2"
                            Margin="0,0,0,5"
                            Content="播放通知音效"
                            IsChecked="{Binding OscPlayNotificationSound}" />
                    </Grid>
                </StackPanel>
            </Expander>

            <StackPanel
                Grid.Row="5" 
                Margin="0,10,0,0"
                HorizontalAlignment="Right"
                Orientation="Horizontal">
                <Button
                    Width="80"
                    Margin="0,0,10,0"
                    Command="{Binding SaveCommand}"
                    Content="保存"
                    IsDefault="True" />
                <Button
                    Width="80"
                    Command="{Binding RevertCommand}"
                    Content="撤销更改" 
                    IsCancel="False" />
            </StackPanel>
        </Grid>
    </ScrollViewer>
</Page>
```
I've wrapped the `Grid` in `ServicePage.xaml` with a `ScrollViewer` because removing the target languages section might make the content shorter, but it's good practice for settings pages that might grow.

**Important Considerations:**

*   **Error Handling**: The current logging adds messages. Ensure all relevant error paths (e.g., OSC init, translation errors) are logged appropriately.
*   **UI Thread Safety**: `AddLogMessage` uses `Application.Current.Dispatcher.Invoke` which is correct. Ensure all UI updates from background threads are similarly dispatched.
*   **Performance**: The `StringJoinConverter` for the log `TextBox` will re-join the entire list of strings every time `LogMessages` changes. For 500 entries, this might become noticeable. A more performant approach for a log would be to use an `ItemsControl` with a `ScrollViewer` and `TextBlock`s, or a custom log control. However, for 500 short lines, the `TextBox` with converter might be acceptable. If performance issues arise, this is an area to optimize.
    *   A quick alternative for the log `TextBox` in `IndexPage.xaml` without a converter, but less ideal MVVM, would be to manage the `TextBox.Text` property directly from the ViewModel by appending and trimming, or in the code-behind. But the converter approach is cleaner for now.
*   **File Map**: The new file `Converters/StringJoinConverter.cs` has been added.

These changes should address your requests. Test thoroughly, especially the settings saving and loading logic between the two pages and the new log display.



You are correct! The error message "XML 命名空间“http://schemas.microsoft.com/winfx/2006/xaml/presentation”中不存在属性“ItemsSource”。 行 142 位置 22." indicates that the `TextBox` control does not have a property named `ItemsSource`. This is because `ItemsSource` is typically used for controls that display a list of items (like `ListBox`, `ListView`, `ItemsControl`), while a `TextBox` is designed to display and edit a single string of text.

In my previous response, I intended to bind the `Text` property of the `TextBox` to your `LogMessages` collection via the `StringJoinConverter`. It seems the `ItemsSource` attribute was mistakenly left on the `TextBox` element, and the `TextBox.Text` block was also present.

Here's the corrected XAML for the log `TextBox` in `Views/Pages/IndexPage.xaml`. You need to remove the `ItemsSource` attribute from the `<TextBox>` tag and simplify the `Text` binding:

```xml
<Page
    x:Class="lingualink_client.Views.IndexPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client.Views"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    xmlns:converters="clr-namespace:lingualink_client.Converters"
    Title="IndexPage"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    mc:Ignorable="d">

    <Grid Margin="10">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" MinWidth="350" /> <!-- Main controls -->
            <ColumnDefinition Width="Auto" /> <!-- GridSplitter -->
            <ColumnDefinition Width="1*" MinWidth="250" /> <!-- Log -->
        </Grid.ColumnDefinitions>

        <!-- Left Column: Main Controls -->
        <Grid Grid.Column="0" Margin="0,0,5,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Mic Selection -->
                <RowDefinition Height="Auto" /> <!-- Target Languages Expander -->
                <RowDefinition Height="Auto" /> <!-- Work Button -->
                <RowDefinition Height="Auto" /> <!-- Status Text -->
                <RowDefinition Height="*" />   <!-- Translation Result -->
                <RowDefinition Height="Auto" /> <!-- Hint -->
            </Grid.RowDefinitions>

            <StackPanel
                Grid.Row="0"
                Margin="0,0,0,10"
                Orientation="Horizontal">
                <Label VerticalAlignment="Center" Content="选择麦克风：" />
                <ComboBox
                    Width="200"
                    Margin="5,0,0,0"
                    VerticalAlignment="Center"
                    DisplayMemberPath="FriendlyName"
                    IsEnabled="{Binding IsMicrophoneComboBoxEnabled}"
                    ItemsSource="{Binding Microphones}"
                    SelectedItem="{Binding SelectedMicrophone}" />
                <Button
                    Margin="10,0,0,0"
                    Padding="5,2"
                    VerticalAlignment="Center"
                    Command="{Binding RefreshMicrophonesCommand}"
                    Content="刷新" />
                <StatusBar
                    Margin="10,0,0,0"
                    HorizontalAlignment="Right"
                    VerticalAlignment="Center"
                    Background="Transparent"
                    Opacity="0.7"
                    Visibility="{Binding IsRefreshingMicrophones, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <StatusBarItem>
                        <TextBlock Text="正在刷新麦克风..." />
                    </StatusBarItem>
                </StatusBar>
            </StackPanel>

            <Expander Grid.Row="1" Header="目标翻译语言" Margin="0,0,0,10" IsExpanded="True">
                <StackPanel Margin="10,5,0,0">
                    <ItemsControl ItemsSource="{Binding TargetLanguageItems}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type vm:SelectableTargetLanguageViewModel}">
                                <Grid Margin="0,3,0,3">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Margin="0,0,8,0" VerticalAlignment="Center" Text="{Binding Label}" />
                                    <ComboBox Grid.Column="1" MinWidth="150" MaxWidth="200" HorizontalAlignment="Stretch"
                                              ItemsSource="{Binding AvailableLanguages}" SelectedItem="{Binding SelectedLanguage}" />
                                    <Button Grid.Column="2" Margin="8,0,0,0" Padding="5,2" VerticalAlignment="Center"
                                            Command="{Binding RemoveCommand}" Content="移除"
                                            Visibility="{Binding CanRemove, Converter={StaticResource BooleanToVisibilityConverter}}" />
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <Button Margin="0,5,0,0" Padding="5,2" HorizontalAlignment="Left"
                            Command="{Binding AddLanguageCommand}" Content="增加语言" />
                </StackPanel>
            </Expander>
            


            <Button
                Grid.Row="2" Margin="0,0,0,10"
                Padding="10,5"
                HorizontalAlignment="Left"
                Command="{Binding ToggleWorkCommand}"
                Content="{Binding WorkButtonContent}" />

            <TextBlock
                Grid.Row="3"
                Margin="0,0,0,10"
                FontSize="14"
                Text="{Binding StatusText}" />

            <TextBox
                Grid.Row="4"
                Margin="0,0,0,10"
                AcceptsReturn="True"
                IsReadOnly="True"
                Text="{Binding TranslationResultText, Mode=OneWay}"
                TextWrapping="Wrap"
                VerticalScrollBarVisibility="Auto" />

            <TextBlock
                Grid.Row="5"
                FontStyle="Italic"
                Foreground="Gray"
                Text="提示：点击“开始工作”后，应用将持续监听麦克风进行VAD检测。" />
        </Grid>

        <!-- GridSplitter -->
        <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Center" VerticalAlignment="Stretch" Background="LightGray" ShowsPreview="True"/>

        <!-- Right Column: Log -->
        
        <Grid Grid.Column="2" Margin="5,0,0,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Log Label & Clear Button -->
                <RowDefinition Height="*" />   <!-- Log TextBox -->
            </Grid.RowDefinitions>

            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,5">
                <Label Content="运行日志:" FontWeight="SemiBold"/>
                <Button Content="清除日志" Command="{Binding ClearLogCommand}" HorizontalAlignment="Right" Margin="10,0,0,0" Padding="5,2"/>
            </StackPanel>
            
            <!-- Corrected TextBox for Log -->
            <TextBox Grid.Row="1"
                     IsReadOnly="True"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto"
                     Text="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}" />
                     <!-- Removed ItemsSource attribute -->
                     <!-- Removed inner TextBox.Text block with MultiBinding -->
        </Grid>
    </Grid>
</Page>
```

**Key changes in the log `TextBox`:**

1.  **Removed `ItemsSource`**: The attribute `ItemsSource="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}"` has been removed from the opening `<TextBox>` tag.
2.  **Simplified `Text` Binding**: The `Text` property is now directly bound: `Text="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}"`.
3.  **Removed `<TextBox.Text>` Block**: The entire `<TextBox.Text> ... </TextBox.Text>` block containing the `MultiBinding` has been removed as it's not needed with the direct `Text` binding.

This corrected version binds the `LogMessages` collection (which is an `ObservableCollection<string>`) to the `Text` property of the `TextBox`. The `StringJoinConverter` will take this collection, join all the strings with newlines, and provide a single string for the `TextBox` to display. This should resolve the XAML error.