好的，这是一份将您的 `lingualink_client` 项目迁移到 `CommunityToolkit.Mvvm` 的完整解决方案。

`CommunityToolkit.Mvvm` 库通过源生成器（Source Generators）显著减少了 MVVM 模式中的样板代码，使得属性的 `INotifyPropertyChanged` 实现和命令的 `ICommand` 实现更加简洁。

**核心变化点：**

1.  **移除 `ViewModelBase` 中的手动 `INotifyPropertyChanged` 实现**：转而继承 `ObservableObject`。
2.  **移除自定义 `DelegateCommand`**：转而使用 `[RelayCommand]` 特性。
3.  **属性变更通知**：将私有字段和公共属性模式替换为 `[ObservableProperty]` 特性。

---

### 迁移步骤

#### 步骤 1: 安装 CommunityToolkit.Mvvm NuGet 包

在 Visual Studio 中，右键点击 `lingualink_client` 项目，选择 "管理 NuGet 程序包..."。
在 "浏览" 选项卡中，搜索 `CommunityToolkit.Mvvm` 并安装最新稳定版本（例如 `8.2.0` 或更高版本）。

#### 步骤 2: 更新 `ViewModelBase.cs`

这个基类将变得非常简洁，因为它不再需要手动实现 `INotifyPropertyChanged` 的逻辑。

**旧 `ViewModels/ViewModelBase.cs` 内容：**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace lingualink_client.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
```

**新 `ViewModels/ViewModelBase.cs` 内容：**

```csharp
using CommunityToolkit.Mvvm.ComponentModel; // 添加此 using

namespace lingualink_client.ViewModels
{
    // 继承 ObservableObject，并将其声明为 partial 类以允许源生成器添加代码
    public abstract partial class ViewModelBase : ObservableObject
    {
        // OnPropertyChanged 和 SetProperty 方法现在由 CommunityToolkit.Mvvm 的源生成器自动处理
        // 你不再需要在这里手动实现它们。
    }
}
```

#### 步骤 3: 移除 `DelegateCommand.cs`

因为我们将替换为 `CommunityToolkit.Mvvm` 的 `[RelayCommand]`，所以您自定义的 `DelegateCommand.cs` 文件可以被删除。

在 `Converters/DelegateCommand.cs` 所在的目录下删除此文件。

#### 步骤 4: 更新各个 ViewModel

这是最主要的修改部分。每个使用 `DelegateCommand` 或手动实现属性变更通知的 ViewModel 都需要更新。

**重要提示：**

*   所有使用 `[ObservableProperty]` 和 `[RelayCommand]` 的 ViewModel 类都必须声明为 `partial`。
*   添加 `using CommunityToolkit.Mvvm.ComponentModel;` 和 `using CommunityToolkit.Mvvm.Input;`。
*   `[ObservableProperty]` 会自动生成公共属性。例如，`[ObservableProperty] private string _name;` 会生成一个名为 `Name` 的公共属性，其 `set` 访问器会自动调用 `OnPropertyChanged`。
*   `[RelRelayCommand]` 会将方法转换为 `ICommand`。例如，`[RelayCommand] private void MyMethod() { ... }` 会生成一个名为 `MyMethodCommand` 的 `ICommand` 属性。
*   命令的 `CanExecute` 逻辑可以通过 `[RelayCommand(CanExecute = nameof(CanMyMethod))]` 来指定，其中 `CanMyMethod` 是一个返回 `bool` 的无参方法。
*   如果属性的 `set` 访问器有额外逻辑（例如，更新其他属性或触发其他方法），可以使用 `[ObservableProperty(OnChanged = nameof(OnMyPropertyChanged))]`，然后实现一个名为 `OnMyPropertyChanged(T oldValue, T newValue)` 的方法。
*   如果命令需要通知 `CanExecute` 状态的改变，使用 `MyMethodCommand.NotifyCanExecuteChanged()`。

---

##### `ViewModels/IndexWindowViewModel.cs`

**旧内容简要回顾：**
- `TargetLanguageItems` 是 `ObservableCollection`。
- `Microphones` 和 `SelectedMicrophone` 使用手动 `SetProperty`。
- `StatusText`, `TranslationResultText`, `WorkButtonContent`, `IsMicrophoneComboBoxEnabled`, `IsRefreshingMicrophones` 使用手动 `SetProperty`。
- `AddLanguageCommand`, `ClearLogCommand`, `RefreshMicrophonesCommand`, `ToggleWorkCommand` 是 `DelegateCommand`。

**新 `ViewModels/IndexWindowViewModel.cs` 内容：**

```csharp
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
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加

namespace lingualink_client.ViewModels
{
    public partial class IndexWindowViewModel : ViewModelBase, IDisposable // 声明为 partial
    {
        private readonly MicrophoneManager _microphoneManager;
        private AudioService _audioService;
        private TranslationService _translationService;
        private readonly SettingsService _settingsService;
        private OscService? _oscService;
        private AppSettings _appSettings;
        
        // Target Language Properties (moved from ServicePageViewModel)
        public ObservableCollection<SelectableTargetLanguageViewModel> TargetLanguageItems { get; }
        // AddLanguageCommand 将被 [RelayCommand] 生成

        private static readonly List<string> AllSupportedLanguages = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
        };
        private const int MaxTargetLanguages = 5;

        // Log Properties
        public ObservableCollection<string> LogMessages { get; }
        public string FormattedLogMessages => string.Join(Environment.NewLine, LogMessages); // New Property
        // ClearLogCommand 将被 [RelayCommand] 生成
        private const int MaxLogEntries = 500;

        // 将属性转换为 [ObservableProperty]
        [ObservableProperty] private ObservableCollection<MMDeviceWrapper> _microphones = new ObservableCollection<MMDeviceWrapper>();

        [ObservableProperty(OnChanged = nameof(OnSelectedMicrophoneChanged))] // 添加 OnChanged 回调
        private MMDeviceWrapper? _selectedMicrophone;

        [ObservableProperty] private string _statusText = "状态：初始化...";
        [ObservableProperty] private string _translationResultText = string.Empty;
        [ObservableProperty] private string _workButtonContent = "开始工作";
        [ObservableProperty] private bool _isMicrophoneComboBoxEnabled = true;

        [ObservableProperty(OnChanged = nameof(OnIsRefreshingMicrophonesChanged))] // 添加 OnChanged 回调
        private bool _isRefreshingMicrophones = false;

        // 语言相关的标签仍然是计算属性，因为它们的值来自 LanguageManager
        public string WorkButtonContentLabel => LanguageManager.GetString("WorkButtonContent");
        public string SelectMicrophoneLabel => LanguageManager.GetString("SelectMicrophone");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string RefreshingMicrophonesLabel => LanguageManager.GetString("RefreshingMicrophones");
        public string TargetLanguagesLabel => LanguageManager.GetString("TargetLanguages");
        public string AddLanguageLabel => LanguageManager.GetString("AddLanguage");
        public string RemoveLabel => LanguageManager.GetString("Remove");
        public string WorkHintLabel => LanguageManager.GetString("WorkHint");

        public IndexWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService();
            
            TargetLanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();
            // AddLanguageCommand 不再需要手动初始化，由 [RelayCommand] 生成

            LogMessages = new ObservableCollection<string>();
            LogMessages.CollectionChanged += OnLogMessagesChanged; // 订阅集合变化
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss.fff}] IndexWindowViewModel Constructor: Log Initialized.");
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] IndexWindowViewModel Constructor: Log Initialized. Count: {LogMessages.Count}");
            // ClearLogCommand 不再需要手动初始化

            // RefreshMicrophonesCommand 和 ToggleWorkCommand 不再需要手动初始化

            LoadSettingsAndInitializeServices(); 
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged;

            _ = ExecuteRefreshMicrophonesAsync();

            // 订阅语言变化，更新依赖语言管理器字符串的属性
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(WorkButtonContentLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SelectMicrophoneLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RefreshLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RefreshingMicrophonesLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(TargetLanguagesLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AddLanguageLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RemoveLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(WorkHintLabel));
        }

        private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FormattedLogMessages)); // 通知格式化字符串属性更新
        }
        
        private void OnGlobalSettingsChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool wasWorking = _audioService?.IsWorking ?? false;
                LoadSettingsAndInitializeServices(true); // This will reload _appSettings and re-init services

                // 智能状态更新逻辑 (简化，因为 LoadSettingsAndInitializeServices 处理 OSC 状态)
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
                ToggleWorkCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
                RefreshMicrophonesCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
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

            RefreshMicrophonesCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
            ToggleWorkCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
        }

        // RelayCommand 方法
        [RelayCommand(CanExecute = nameof(CanExecuteRefreshMicrophones))] // 绑定 CanExecute 方法
        private async Task ExecuteRefreshMicrophonesAsync() // 方法名与命令名对应
        {
            IsRefreshingMicrophones = true; // [ObservableProperty] 自动通知属性更改
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
                
                // OnSelectedMicrophoneChanged() 会通过 SelectedMicrophone 属性的 OnChanged 触发
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
                IsRefreshingMicrophones = false; // [ObservableProperty] 自动通知属性更改
                ToggleWorkCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
            }
        }

        private bool CanExecuteRefreshMicrophones() => !_audioService.IsWorking && !IsRefreshingMicrophones;

        // SelectedMicrophone 属性的 OnChanged 回调
        private void OnSelectedMicrophoneChanged(MMDeviceWrapper? oldValue, MMDeviceWrapper? newValue)
        {
             if (newValue != null)
            {
                if (newValue.WaveInDeviceIndex != -1 && newValue.WaveInDeviceIndex < WaveIn.DeviceCount)
                {
                     if (!StatusText.Contains("OSC服务") && !StatusText.Contains("设置已更新") && !StatusText.Contains("正在刷新") && !_audioService.IsWorking)
                        StatusText = $"状态：已选择麦克风: {newValue.FriendlyName}";
                }
                else
                {
                    int cbIndex = Microphones.IndexOf(newValue);
                    if (cbIndex >= 0 && cbIndex < WaveIn.DeviceCount) {
                        newValue.WaveInDeviceIndex = cbIndex;
                         StatusText = $"状态：已选择麦克风 (回退索引): {newValue.FriendlyName}";
                    } else {
                        StatusText = $"状态：麦克风 '{newValue.FriendlyName}' 无效。";
                        SelectedMicrophone = null; // 重新设置属性，会再次触发 OnChanged (但newValue会是null)
                    }
                }
            } else if (!Microphones.Any()){
                 StatusText = "状态：未找到可用的麦克风设备。请刷新或检查设备。";
            }
            ToggleWorkCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
        }

        // IsRefreshingMicrophones 属性的 OnChanged 回调
        private void OnIsRefreshingMicrophonesChanged(bool oldValue, bool newValue)
        {
            RefreshMicrophonesCommand.NotifyCanExecuteChanged(); // 刷新 RefreshMicrophonesCommand 的 CanExecute 状态
            ToggleWorkCommand.NotifyCanExecuteChanged(); // 刷新 ToggleWorkCommand 的 CanExecute 状态
        }

        [RelayCommand(CanExecute = nameof(CanExecuteToggleWork))] // 绑定 CanExecute 方法
        private async Task ExecuteToggleWorkAsync() // 方法名与命令名对应
        {
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

            RefreshMicrophonesCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
            ToggleWorkCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
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
            var (response, rawJsonResponse, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty;
            string logEntry;

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
                    TranslationResultText = response.Data.Raw_Text;
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
            AddLanguageCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
        }
        
        // RelayCommand 方法
        [RelayCommand(CanExecute = nameof(CanExecuteAddLanguage))] // 绑定 CanExecute 方法
        private void ExecuteAddLanguage() // 方法名与命令名对应，无需参数
        {
            if (!CanExecuteAddLanguage()) return;
            string defaultNewLang = AllSupportedLanguages.FirstOrDefault(l => !TargetLanguageItems.Any(item => item.SelectedLanguage == l))
                                    ?? AllSupportedLanguages.First(); 
            var newItem = new SelectableTargetLanguageViewModel(this, defaultNewLang, new List<string>(AllSupportedLanguages));
            TargetLanguageItems.Add(newItem);
            UpdateItemPropertiesAndAvailableLanguages();
            AddLanguageCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
            SaveCurrentSettings(); // Save when target languages change
        }

        private bool CanExecuteAddLanguage() // 方法名与命令名对应，无需参数
        {
            return TargetLanguageItems.Count < MaxTargetLanguages;
        }

        public void RemoveLanguageItem(SelectableTargetLanguageViewModel itemToRemove)
        {
            if (TargetLanguageItems.Contains(itemToRemove))
            {
                TargetLanguageItems.Remove(itemToRemove);
                UpdateItemPropertiesAndAvailableLanguages();
                AddLanguageCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
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

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void ExecuteClearLog() // 方法名与命令名对应，无需参数
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
                LogMessages.CollectionChanged -= OnLogMessagesChanged; // 取消订阅
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

---

##### `ViewModels/MainWindowViewModel.cs`

这个 ViewModel 只有 `LanguageManager.GetString` 这样的计算属性，不需要 `[ObservableProperty]` 或 `[RelayCommand]`。现有代码已经符合 MVVM 最佳实践，无需改动。

**`ViewModels/MainWindowViewModel.cs` 无需修改。**

---

##### `ViewModels/SelectableTargetLanguageViewModel.cs`

**旧内容简要回顾：**
- `SelectedLanguage`, `AvailableLanguages`, `Label`, `CanRemove` 使用手动 `SetProperty`。
- `RemoveCommand` 是 `DelegateCommand`。

**新 `ViewModels/SelectableTargetLanguageViewModel.cs` 内容：**

```csharp
using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加

namespace lingualink_client.ViewModels
{
    public partial class SelectableTargetLanguageViewModel : ViewModelBase // 声明为 partial
    {
        public IndexWindowViewModel ParentViewModel { get; }

        [ObservableProperty(OnChanged = nameof(OnSelectedLanguageChanged))] // 添加 OnChanged 回调
        private string _selectedLanguage;

        [ObservableProperty] // 这里 collection 实例本身可以作为 ObservableProperty (尽管通常是其内容变化)
        private ObservableCollection<string> _availableLanguages;

        [ObservableProperty] private string _label; // Backing field for Label

        public string LabelText => LanguageManager.GetString("TargetLanguageLabel"); // 这是一个计算属性，不是 [ObservableProperty]

        [ObservableProperty(OnChanged = nameof(OnCanRemoveChanged))] // 添加 OnChanged 回调
        private bool _canRemove;

        // RemoveCommand 将被 [RelayCommand] 生成
        // public DelegateCommand RemoveCommand { get; } // 移除此行

        public SelectableTargetLanguageViewModel(IndexWindowViewModel parent, string initialSelection, List<string> allLangsSeed)
        {
            ParentViewModel = parent;
            _availableLanguages = new ObservableCollection<string>(allLangsSeed);
            _selectedLanguage = initialSelection; // 初始赋值给 backing field

            // RemoveCommand 不再需要手动初始化，由 [RelayCommand] 生成
            // No need to initialize RemoveCommand here

            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LabelText));
        }

        // SelectedLanguage 属性的 OnChanged 回调
        private void OnSelectedLanguageChanged(string oldValue, string newValue)
        {
            ParentViewModel?.OnLanguageSelectionChanged(this);
        }

        // CanRemove 属性的 OnChanged 回调
        private void OnCanRemoveChanged(bool oldValue, bool newValue)
        {
            RemoveCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
        }

        // RelayCommand 方法
        [RelayCommand(CanExecute = nameof(CanRemove))] // 绑定 CanExecute 方法，使用 CanRemove 属性作为 CanExecute 条件
        private void RemoveItem() // 方法名与命令名对应
        {
            ParentViewModel.RemoveLanguageItem(this);
        }
    }
}
```

---

##### `ViewModels/ServicePageViewModel.cs`

**旧内容简要回顾：**
- 所有设置相关的属性（`ServerUrl` 等）都使用手动 `SetProperty`。
- `SaveCommand`, `RevertCommand` 是 `DelegateCommand`。

**新 `ViewModels/ServicePageViewModel.cs` 内容：**

```csharp
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

        // 将属性转换为 [ObservableProperty]
        [ObservableProperty] private string _serverUrl;
        [ObservableProperty] private double _silenceThresholdSeconds;
        [ObservableProperty] private double _minVoiceDurationSeconds;
        [ObservableProperty] private double _maxVoiceDurationSeconds;
        [ObservableProperty(OnChanged = nameof(OnMinRecordingVolumeThresholdChanged))] // 添加 OnChanged 回调进行值限制
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
        }

        // MinRecordingVolumeThreshold 属性的 OnChanged 回调
        private void OnMinRecordingVolumeThresholdChanged(double oldValue, double newValue)
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
                MessageBox.Show("服务器URL无效。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (SilenceThresholdSeconds <= 0) { MessageBox.Show("静音检测阈值必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds <= 0) { MessageBox.Show("最小语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MaxVoiceDurationSeconds <= 0) { MessageBox.Show("最大语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            if (MinVoiceDurationSeconds >= MaxVoiceDurationSeconds) { MessageBox.Show("最小语音时长必须小于最大语音时长。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
            // MinRecordingVolumeThreshold 的验证现在由 OnChanged 方法处理，这里不需要重复进行硬性检查

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
                    SettingsChangedNotifier.RaiseSettingsChanged(); // 通知其他部分设置已更改
                    MessageBox.Show("服务相关设置已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // RelayCommand 方法
        [RelayCommand] // 标记为 RelayCommand
        private void ExecuteRevertSettings() // 方法名与命令名对应，无需参数
        {
            // 重新加载所有设置，包括可能由 IndexPage 更改的目标语言
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings); // This will only load service-specific parts into UI
            MessageBox.Show("更改已撤销，设置已从上次保存的状态重新加载。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
```

---

##### `ViewModels/SettingPageViewModel.cs`

这个 ViewModel 也只有 `InterfaceLanguage` 这样的计算属性，不需要 `[ObservableProperty]` 或 `[RelayCommand]`。现有代码已经符合 MVVM 最佳实践，无需改动。

**`ViewModels/SettingPageViewModel.cs` 无需修改。**

---

#### 步骤 5: 验证 XAML 绑定

由于 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 会生成与您手动实现的属性同名的公共属性，并且 `[RelayCommand]` 会生成 `[MethodName]Command` 形式的 `ICommand` 属性，所以大部分 XAML 绑定无需更改。

您需要确保在 `IndexPage.xaml` 和 `LogPage.xaml` 中，命令绑定正确地指向了新的命令名称：

*   `IndexPage.xaml`:
    *   `Command="{Binding RefreshMicrophonesCommand}"` (保持不变)
    *   `Command="{Binding ToggleWorkCommand}"` (保持不变)
    *   `Command="{Binding AddLanguageCommand}"` (保持不变)
    *   `Command="{Binding RemoveCommand}"` (在 `SelectableTargetLanguageViewModel` 中，它现在是一个 `RelayCommand`，名称保持不变，但其方法变为 `RemoveItem` )

*   `LogPage.xaml`:
    *   `Command="{Binding ClearLogCommand}"` (保持不变)

**XAML 文件中，命令名称的绑定保持不变，因为源生成器会生成名为 `OriginalMethodNameCommand` 的公共 `ICommand` 属性。**

---

### 完成与测试

1.  **保存所有文件。**
2.  **重建解决方案 (Rebuild Solution)**：这会触发 CommunityToolkit.Mvvm 的源生成器，生成必要的属性和命令代码。如果存在任何编译错误，请仔细检查上述步骤中 `partial` 关键字、`using` 语句、`[ObservableProperty]` 和 `[RelayCommand]` 的使用是否正确。
3.  **运行应用程序。**
4.  **全面测试所有功能**：
    *   麦克风选择和刷新。
    *   开始/停止工作按钮。
    *   翻译结果显示。
    *   日志页面的日志显示和清除功能。
    *   目标语言的添加、删除和选择更改。
    *   服务设置页面中的所有参数（服务器 URL、VAD 参数、OSC 设置）的保存和撤销。
    *   设置页面中的界面语言切换。
    *   主窗口的主题切换。

通过以上步骤，您的 `lingualink_client` 项目将成功迁移到 `CommunityToolkit.Mvvm`，从而获得更简洁、高效的 MVVM 代码。