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