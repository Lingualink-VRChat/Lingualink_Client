Okay, I'll help you get the logging functionality working correctly and enhance it to show the full server response.

Here's a breakdown of the changes:

**1. Fix Log Display and Clear Button Functionality**

The most robust way to handle a collection of log messages being displayed in a single `TextBox` is to have a ViewModel property that provides the already formatted string. This property will be updated whenever the log collection changes.

**File: `ViewModels/IndexWindowViewModel.cs`**
We'll add a new property `FormattedLogMessages` and update it whenever `LogMessages` changes.

```csharp
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
using System.Diagnostics; // Add this for Debug.WriteLine
using System.Collections.Specialized; // Required for INotifyCollectionChanged

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
        public string FormattedLogMessages => string.Join(Environment.NewLine, LogMessages); // New Property
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
            LogMessages.CollectionChanged += OnLogMessagesChanged; // Subscribe to changes
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss.fff}] IndexWindowViewModel Constructor: Log Initialized.");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IndexWindowViewModel Constructor: Log Initialized. Count: {LogMessages.Count}");
            ClearLogCommand = new DelegateCommand(ExecuteClearLog);
            
            RefreshMicrophonesCommand = new DelegateCommand(async _ => await ExecuteRefreshMicrophonesAsync(), _ => CanExecuteRefreshMicrophones());
            ToggleWorkCommand = new DelegateCommand(async _ => await ExecuteToggleWorkAsync(), _ => CanExecuteToggleWork());

            LoadSettingsAndInitializeServices(); 
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged;

            _ = ExecuteRefreshMicrophonesAsync(); 
        }

        private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FormattedLogMessages)); // Notify that the formatted string needs to update
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
                AddLogMessage($"AudioService Status: {status}");
            });
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            string currentUiStatus = $"状态：正在发送片段 ({e.TriggerReason})...";
            Application.Current.Dispatcher.Invoke(() => StatusText = currentUiStatus);
            AddLogMessage($"发送片段 ({e.TriggerReason}, {e.AudioData.Length} bytes) at {DateTime.Now:HH:mm:ss}");


            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            // Modified: Capture rawJsonResponse
            var (response, rawJsonResponse, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty;
            string logEntry;

            // New: Log raw server response if available
            if (!string.IsNullOrEmpty(rawJsonResponse))
            {
                AddLogMessage($"服务端原始响应 ({e.TriggerReason}): {rawJsonResponse}");
            }

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
                AddLogMessage(logEntry); // This AddLogMessage now triggers OnPropertyChanged for FormattedLogMessages
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
            Debug.WriteLine($"AddLogMessage INVOKED with: \"{message}\". Current LogMessages count before add: {LogMessages.Count}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                LogMessages.Add(timestampedMessage); // This will trigger OnLogMessagesChanged
                Debug.WriteLine($"LogMessages UPDATED. New count: {LogMessages.Count}. Last message: \"{LogMessages.LastOrDefault()}\"");
                while (LogMessages.Count > MaxLogEntries)
                {
                    LogMessages.RemoveAt(0); // This will also trigger OnLogMessagesChanged
                }
            });
        }

        private void ExecuteClearLog(object? parameter)
        {
            Debug.WriteLine($"ExecuteClearLog INVOKED. LogMessages count BEFORE clear: {LogMessages.Count}");
            LogMessages.Clear(); // This will trigger OnLogMessagesChanged (with Action=Reset)
            Debug.WriteLine($"ExecuteClearLog: LogMessages.Clear() called. LogMessages count AFTER clear: {LogMessages.Count}");
            AddLogMessage("日志已清除。"); // This will add one message and trigger OnLogMessagesChanged
        }

        public void Dispose()
        {
            SettingsChangedNotifier.SettingsChanged -= OnGlobalSettingsChanged;
            if (LogMessages != null)
            {
                LogMessages.CollectionChanged -= OnLogMessagesChanged; // Unsubscribe
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
```

**File: `Views/Pages/LogPage.xaml`**
Update the `TextBox` binding to use `FormattedLogMessages` and remove the converter.

```xml
<Page
    x:Class="lingualink_client.Views.LogPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    mc:Ignorable="d"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    Title="LogPage">

    <Grid Margin="15"> <!-- Increased margin for a page view -->
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" /> <!-- Log Label & Clear Button -->
            <RowDefinition Height="*" />   <!-- Log TextBox -->
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10"> <!-- Adjusted margin -->
            <Label Content="运行日志:" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
            <Button Content="清除日志" Command="{Binding ClearLogCommand}" HorizontalAlignment="Right" Margin="15,0,0,0" Padding="8,4" VerticalAlignment="Center"/>
        </StackPanel>
        
        <TextBox Grid.Row="1"
                 IsReadOnly="True"
                 AcceptsReturn="True"
                 TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto"
                 Text="{Binding FormattedLogMessages, Mode=OneWay}" 
                 FontFamily="Consolas" FontSize="12"/>
    </Grid>
</Page>
```

**2. Enhance Logging with Full Backend Response**

**File: `Services/TranslationService.cs`**
Modify the `TranslateAudioSegmentAsync` method to return the raw JSON string.

```csharp
// lingualink_client.Services.TranslationService.cs
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Text.Json;
using lingualink_client.Models;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace lingualink_client.Services
{
    public class TranslationService : IDisposable
    {
        private readonly string _serverUrl;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        public TranslationService(string serverUrl)
        {
            _serverUrl = serverUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        // Modified return type
        public async Task<(ServerResponse? Response, string? RawJsonResponse, string? ErrorMessage)> TranslateAudioSegmentAsync(
            byte[] audioData,
            WaveFormat waveFormat,
            string triggerReason,
            string targetLanguagesCsv)
        {
            if (audioData.Length == 0)
            {
                return (null, null, "Audio data is empty.");
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.wav");
            string? responseContentString = null; // To store raw JSON
            try
            {
                await using (var writer = new WaveFileWriter(tempFilePath, waveFormat))
                {
                    await writer.WriteAsync(audioData, 0, audioData.Length);
                }

                using (var formData = new MultipartFormDataContent())
                {
                    var fileBytes = await File.ReadAllBytesAsync(tempFilePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    formData.Add(fileContent, "audio_file", Path.GetFileName(tempFilePath));

                    if (!string.IsNullOrWhiteSpace(targetLanguagesCsv))
                    {
                        var languagesList = targetLanguagesCsv.Split(',')
                                                              .Select(lang => lang.Trim())
                                                              .Where(lang => !string.IsNullOrWhiteSpace(lang));
                        foreach (var lang in languagesList)
                        {
                            Debug.WriteLine($"[DEBUG] TranslationService: 添加 'target_languages' = '{lang}'");
                            formData.Add(new StringContent(lang), "target_languages");
                        }
                    }

                    var httpResponse = await _httpClient.PostAsync(_serverUrl, formData);
                    responseContentString = await httpResponse.Content.ReadAsStringAsync(); // Get raw JSON

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        try
                        {
                            var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            return (serverResponse, responseContentString, null); // Return raw JSON
                        }
                        catch (JsonException ex)
                        {
                            return (null, responseContentString, $"Failed to deserialize server success response: {ex.Message}. Response: {responseContentString}");
                        }
                    }
                    else
                    {
                        try
                        {
                            var errorResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                            {
                                return (errorResponse, responseContentString, $"Server error ({httpResponse.StatusCode}): {errorResponse.Message}");
                            }
                        }
                        catch { }
                        return (null, responseContentString, $"Server error ({httpResponse.StatusCode}): {responseContentString}");
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                return (null, responseContentString, $"Network request timed out: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return (null, responseContentString, $"Network request error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, responseContentString, $"Error processing/sending segment: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
```

**3. Optional: Remove Unused `StringJoinConverter`**

Since `StringJoinConverter` is no longer used by `LogPage.xaml` (assuming it's not used anywhere else), you can remove it.

*   **Delete the file:** `Converters/StringJoinConverter.cs`
*   **Remove from `App.xaml`:**
    Delete this line from `App.xaml`'s `ResourceDictionary`:
    ```xml
    <converters:StringJoinConverter x:Key="StringJoinConverter" />
    ```
    And if the `converters` namespace alias is only for this, you can remove `xmlns:converters="clr-namespace:lingualink_client.Converters"` as well, but it's also used for `BooleanToVisibilityConverter`, so keep the namespace alias.

With these changes:
1.  The log messages should now appear correctly in the LogPage.
2.  The "清除日志" (Clear Log) button should work, clearing the display and adding a "日志已清除" message.
3.  When a translation occurs, the raw JSON response from the server will be logged before the parsed summary.