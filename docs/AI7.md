好的，实现一个完善的多语言系统确实需要一些步骤。我们将使用 .NET 的标准资源文件 (.resx) 机制。

**核心思路：**

1.  **创建资源文件**：为每种支持的语言（中文、英文、日文等）创建 `.resx` 文件，其中包含所有UI文本的键值对。
2.  **在XAML中使用资源**：通过 `x:Static` 标记扩展将XAML控件的文本属性绑定到资源文件中的键。
3.  **在C#代码中使用资源**：在ViewModel或代码隐藏中，通过生成的资源类访问文本。
4.  **语言切换逻辑**：
    *   在 `App.xaml.cs` 中，根据用户设置在程序启动时设置当前线程的UI区域性 (`CurrentUICulture`)。
    *   在设置页面，允许用户选择语言，保存该设置。
    *   当语言更改后，提示用户重启应用程序以使所有更改生效（这是最简单直接的方式，动态刷新所有UI比较复杂）。

**步骤详解：**

**第一步：创建资源文件**

1.  在你的项目 (`lingualink_client`) 上右键 -> **添加** -> **新建项**。
2.  选择 **资源文件**，命名为 `Strings.resx`。这个将是你的默认语言资源（比如中文）。
    *   打开 `Strings.resx` 文件，在顶部的工具栏中，将 **访问修饰符 (Access Modifier)** 设置为 `Public`。这会生成一个 `public class Strings`。
3.  再次右键项目 -> **添加** -> **新建项** -> **资源文件**。
    *   命名为 `Strings.en-US.resx` (用于英文 - 美国)。同样设置访问修饰符为 `Public`。
    *   命名为 `Strings.ja-JP.resx` (用于日文 - 日本)。同样设置访问修饰符为 `Public`。

**第二步：填充资源文件**

1.  **打开 `Strings.resx` (中文 - 默认)**：
    为所有需要在UI上显示的文本添加条目。**名称 (Name)** 是键，**值 (Value)** 是对应的中文文本。

    例如：
    | 名称                                       | 值                         | 备注                       |
    | :----------------------------------------- | :------------------------- | :------------------------- |
    | `MainWindow_Title`                         | `VRChat LinguaLink - 客户端` | 主窗口标题                 |
    | `NavigationViewItem_Start`                 | `启动`                     | 导航项 - 启动              |
    | `NavigationViewItem_Service`               | `服务`                     | 导航项 - 服务              |
    | `NavigationViewItem_Log`                   | `日志`                     | 导航项 - 日志              |
    | `NavigationViewItem_Settings`              | `设置`                     | 导航项 - 设置              |
    | `IndexPage_SelectMicrophoneLabel`          | `选择麦克风：`             | IndexPage - 选择麦克风标签 |
    | `IndexPage_RefreshButtonContent`           | `刷新`                     | IndexPage - 刷新按钮       |
    | `IndexPage_TargetLanguagesHeader`          | `目标翻译语言`             | IndexPage - 目标语言展开器 |
    | `IndexPage_AddLanguageButtonContent`       | `增加语言`                 | IndexPage - 增加语言按钮   |
    | `IndexPage_RemoveLanguageButtonContent`    | `移除`                     | IndexPage - 移除语言按钮   |
    | `IndexPage_WorkButton_Start`               | `开始工作`                 | 开始工作按钮文本           |
    | `IndexPage_WorkButton_Stop`                | `停止工作`                 | 停止工作按钮文本           |
    | `IndexPage_Status_Initializing`            | `状态：初始化...`          | 初始状态文本               |
    | `IndexPage_Status_MicListRefreshed`        | `状态：麦克风列表已刷新。` |                            |
    | `IndexPage_Status_MicSelected`             | `状态：已选择麦克风: {0}`  | {0} 是麦克风名称           |
    | `IndexPage_Status_Listening`               | `状态：正在监听...`        |                            |
    | `IndexPage_Status_VoiceDetected`           | `状态：检测到语音...`      |                            |
    | `IndexPage_Status_SilenceDetected`         | `状态：检测到静音，准备处理...`|                            |
    | `IndexPage_Status_SegmentTooShort`         | `状态：语音片段过短，已忽略。正在监听...`|                  |
    | `IndexPage_Status_SendingSegment`          | `状态：正在发送片段 ({0})...`| {0} 是触发原因             |
    | `IndexPage_Status_TranslationSuccess`      | `状态：翻译成功！`         |                            |
    | `IndexPage_Status_TranslationSuccessOSC`   | `状态：翻译成功！已发送到VRChat。`|                         |
    | `IndexPage_Status_TranslationFailed`       | `状态：翻译请求失败。`     |                            |
    | `IndexPage_Status_TranslationServerError`  | `状态：翻译失败 (服务器)。`|                            |
    | `IndexPage_Status_TranslationEmpty`        | `状态：翻译成功，但无文本内容。`|                         |
    | `IndexPage_Status_OSCSendFailed`           | `状态：翻译成功！但VRChat发送失败: {0}`| {0} 错误信息        |
    | `IndexPage_MicrophoneRefreshInProgress`    | `正在刷新麦克风...`        |                            |
    | `IndexPage_Hint_VAD`                       | `提示：点击“开始工作”后，应用将持续监听麦克风进行VAD检测。` | |
    | `LogPage_Title`                            | `运行日志:`                | LogPage - 标题             |
    | `LogPage_ClearLogButtonContent`            | `清除日志`                 | LogPage - 清除按钮         |
    | `ServicePage_ServerUrlLabel`               | `服务器 URL:`              | ServicePage - 服务器URL    |
    | `ServicePage_AdvancedVADHeader`            | `高级VAD设置`              | ServicePage - VAD设置      |
    | `ServicePage_SilenceThresholdLabel`        | `静音检测阈值 (秒):`       |                            |
    | `ServicePage_MinVoiceDurationLabel`        | `最小语音时长 (秒):`       |                            |
    | `ServicePage_MaxVoiceDurationLabel`        | `最大语音时长 (秒):`       |                            |
    | `ServicePage_MinRecordingVolumeLabel`      | `最小录音音量阈值 (0-100%):`|                            |
    | `ServicePage_MinRecordingVolumeHint`       | `提示: 仅当麦克风输入音量高于此阈值时才开始处理语音。0% 表示禁用此过滤。` | |
    | `ServicePage_OSCSettingsHeader`            | `VRChat OSC 发送设置`      |                            |
    | `ServicePage_EnableOSCCheckbox`            | `启用 OSC 发送`            |                            |
    | `ServicePage_OSCIPAddressLabel`            | `OSC IP 地址:`             |                            |
    | `ServicePage_OSCPortLabel`                 | `OSC 端口:`                |                            |
    | `ServicePage_OSCSendImmediatelyCheckbox`   | `立即发送 (绕过键盘)`      |                            |
    | `ServicePage_OSCPlayNotificationCheckbox`  | `播放通知音效`             |                            |
    | `ServicePage_SaveButtonContent`            | `保存`                     |                            |
    | `ServicePage_RevertButtonContent`          | `撤销更改`                 |                            |
    | `SettingPage_Title`                        | `设置`                     | (如果导航项不足以代表页面标题) |
    | `SettingPage_InterfaceLanguageLabel`       | `界面语言：`               |                            |
    | `MessageBox_Error_Title`                   | `错误`                     | 消息框标题 - 错误          |
    | `MessageBox_Success_Title`                 | `成功`                     | 消息框标题 - 成功          |
    | `MessageBox_Information_Title`             | `提示`                     | 消息框标题 - 信息          |
    | `MessageBox_SelectValidMicrophone`         | `请选择一个有效的麦克风设备。`|                            |
    | `MessageBox_InvalidServerUrl`              | `服务器URL无效。`          |                            |
    | `MessageBox_SettingsSaved`                 | `服务相关设置已保存。`     |                            |
    | `MessageBox_SettingsReverted`              | `更改已撤销，设置已从上次保存的状态重新加载。` |             |
    | `MessageBox_LanguageChangeRestart_Text`    | `语言设置已更改。请重新启动应用程序以使更改完全生效。` |     |
    | `MessageBox_LanguageChangeRestart_Caption` | `需要重启`                 |                            |
    | `Log_Cleared`                              | `日志已清除。`             |                            |
    | `Log_TargetLanguageUpdated`                | `目标语言设置已更新并保存。`|                            |
    | `Log_OSCInitFailed`                        | `OSC服务初始化失败: {0}`   | {0} 错误信息        |
    | `Log_MicRefreshFailed`                     | `刷新麦克风列表失败: {0}`  | {0} 错误信息        |
    | `Log_AudioServiceStatus`                   | `AudioService Status: {0}` | {0} 状态信息        |
    | `Log_TranslationError`                     | `翻译错误 ({0}): {1}`      | {0}原因 {1}错误     |
    | `Log_TranslationSuccessNoText`             | `翻译成功但无文本 ({0}). (LLM: {1:F2}s)` | {0}原因 {1}时长 |
    | `Log_TranslationServerProcessingError`     | `服务器处理错误 ({0}): {1}` | {0}原因 {1}错误     |
    | `Log_TranslationEmptyResponse`             | `收到空响应 ({0})`         | {0}原因                 |
    | `Log_OSCSent`                              | `[OSC] 消息已发送到VRChat: \"{0}...\"` | {0} 消息预览      |
    | `Log_OSCSendError`                         | `[OSC ERROR] 发送失败: {0}`| {0} 错误信息        |
    | `Log_RawServerResponse`                    | `服务端原始响应 ({0}): {1}`| {0}原因 {1}JSON     |

2.  **打开 `Strings.en-US.resx` (英文)** 和 **`Strings.ja-JP.resx` (日文)**：
    *   将 `Strings.resx` 中的所有 **名称 (Name)** 列复制到这两个文件中。
    *   然后，为每个名称填写对应的英文和日文 **值 (Value)**。

**第三步：修改 App.xaml.cs 以设置区域性**

```csharp
// App.xaml.cs
using lingualink_client.ViewModels;
using System.Windows;
using lingualink_client.Services; // For SettingsService
using System.Globalization;       // For CultureInfo
using System.Threading;           // For Thread
using System.Diagnostics;         // For Debug

namespace lingualink_client
{
    public partial class App : Application
    {
        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; }
        private SettingsService _settingsService;

        protected override void OnStartup(StartupEventArgs e)
        {
            _settingsService = new SettingsService();
            var settings = _settingsService.LoadSettings();

            // 设置区域性，必须在任何UI元素创建之前
            SetCulture(settings.Language);

            base.OnStartup(e);
            SharedIndexWindowViewModel = new IndexWindowViewModel(); // ViewModel现在会使用正确的区域性来初始化其内部字符串

            // MainWindow 会在 StartupUri="MainWindow.xaml" 中被创建, 它将使用已设置的区域性
        }

        public void SetCulture(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
            {
                cultureName = "zh-CN"; // 默认回退
            }
            try
            {
                CultureInfo culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                Debug.WriteLine($"App culture set to: {cultureName}");
            }
            catch (CultureNotFoundException ex)
            {
                Debug.WriteLine($"Culture '{cultureName}' not found. Falling back to zh-CN. Error: {ex.Message}");
                CultureInfo culture = new CultureInfo("zh-CN"); // 安全回退
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SharedIndexWindowViewModel?.Dispose(); // Ensure ViewModel is disposed
            base.OnExit(e);
        }
    }
}
```

**第四步：修改 AppSettings.cs**

```csharp
// Models/AppSettings.cs
namespace lingualink_client.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "zh-CN"; // 默认语言，例如 "en-US", "ja-JP"
        public string TargetLanguages { get; set; } = "英文,日文";
        public string ServerUrl { get; set; } = "http://localhost:5000/translate_audio";

        // VAD Parameters
        public double SilenceThresholdSeconds { get; set; } = 0.8;
        public double MinVoiceDurationSeconds { get; set; } = 0.8;
        public double MaxVoiceDurationSeconds { get; set; } = 10.0;
        public double MinRecordingVolumeThreshold { get; set; } = 0.05;

         // OSC Settings
        public bool EnableOsc { get; set; } = false;
        public string OscIpAddress { get; set; } = "127.0.0.1";
        public int OscPort { get; set; } = 9000;
        public bool OscSendImmediately { get; set; } = true;
        public bool OscPlayNotificationSound { get; set; } = false;
    }
}
```
`SettingsService` 不需要更改，`JsonSerializer` 会自动处理新增的 `Language` 属性。

**第五步：创建 SettingPageViewModel 和 LanguageItem 模型**

1.  **Models/LanguageItem.cs** (新文件)
    ```csharp
    namespace lingualink_client.Models
    {
        public class LanguageItem
        {
            public string Name { get; set; } // 显示给用户的名称, e.g., "中文 (简体)"
            public string CultureCode { get; set; } // e.g., "zh-CN"

            public LanguageItem(string name, string cultureCode)
            {
                Name = name;
                CultureCode = cultureCode;
            }

            // ComboBox 会用 ToString() 来显示，除非设置了 DisplayMemberPath
            public override string ToString() => Name;
        }
    }
    ```

2.  **ViewModels/SettingPageViewModel.cs** (新文件)
    ```csharp
    using lingualink_client.Models;
    using lingualink_client.Services;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows; // For MessageBox
    // 确保引用 Properties 命名空间以访问资源
    using lingualink_client.Properties;


    namespace lingualink_client.ViewModels
    {
        public class SettingPageViewModel : ViewModelBase
        {
            private readonly SettingsService _settingsService;
            private AppSettings _currentSettings;

            public ObservableCollection<LanguageItem> AvailableLanguages { get; }

            private LanguageItem _selectedLanguage;
            public LanguageItem SelectedLanguage
            {
                get => _selectedLanguage;
                set
                {
                    if (SetProperty(ref _selectedLanguage, value))
                    {
                        ApplyLanguageChange();
                    }
                }
            }

            public SettingPageViewModel(SettingsService settingsService)
            {
                _settingsService = settingsService;
                _currentSettings = _settingsService.LoadSettings();

                AvailableLanguages = new ObservableCollection<LanguageItem>
                {
                    // 名称可以从资源文件读取，如果希望语言选择本身也是本地化的
                    new LanguageItem("中文", "zh-CN"),
                    new LanguageItem("English", "en-US"),
                    new LanguageItem("日本語", "ja-JP")
                };

                // 根据保存的设置初始化选择
                _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.CultureCode == _currentSettings.Language)
                                   ?? AvailableLanguages.First(l => l.CultureCode == "zh-CN"); // 安全回退
            }

            private void ApplyLanguageChange()
            {
                if (_selectedLanguage != null && _selectedLanguage.CultureCode != _currentSettings.Language)
                {
                    _currentSettings.Language = _selectedLanguage.CultureCode;
                    _settingsService.SaveSettings(_currentSettings);
                    SettingsChangedNotifier.RaiseSettingsChanged(); // 虽然这个 notifier 目前主要影响 IndexWindowViewModel，但保留无妨

                    // 使用资源字符串显示消息框
                    MessageBox.Show(
                        Properties.Strings.MessageBox_LanguageChangeRestart_Text,
                        Properties.Strings.MessageBox_LanguageChangeRestart_Caption,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }
    }
    ```

**第六步：修改 SettingPage.xaml 和 SettingPage.xaml.cs**

1.  **Views/Pages/SettingPage.xaml**:
    ```xml
    <Page
        x:Class="lingualink_client.Views.SettingPage"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:local="clr-namespace:lingualink_client.Views"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:vm="clr-namespace:lingualink_client.ViewModels"
        xmlns:p="clr-namespace:lingualink_client.Properties" <!-- 添加这个命名空间 -->
        Title="{x:Static p:Strings.NavigationViewItem_Settings}" <!-- 页面标题也可以本地化 -->
        d:DataContext="{d:DesignInstance Type=vm:SettingPageViewModel}"
        mc:Ignorable="d">
        <Grid Margin="15,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="auto" />
                <RowDefinition Height="auto" />
                <RowDefinition />
            </Grid.RowDefinitions>

            <Label Content="{x:Static p:Strings.SettingPage_InterfaceLanguageLabel}" />

            <ComboBox
                Grid.Row="1"
                VerticalAlignment="Stretch"
                ItemsSource="{Binding AvailableLanguages}"
                SelectedItem="{Binding SelectedLanguage}"
                DisplayMemberPath="Name" /> <!-- LanguageItem.Name 将用于显示 -->
        </Grid>
    </Page>
    ```

2.  **Views/Pages/SettingPage.xaml.cs**:
    ```csharp
    using lingualink_client.Services;
    using lingualink_client.ViewModels;
    using System.Windows.Controls;

    namespace lingualink_client.Views
    {
        public partial class SettingPage : Page
        {
            public SettingPage()
            {
                InitializeComponent();
                // 建议使用依赖注入，但这里为了简单直接实例化
                DataContext = new SettingPageViewModel(new SettingsService());
            }
        }
    }
    ```

**第七步：修改所有XAML文件以使用资源**

在每个XAML文件的顶部（如 `Window` 或 `Page` 标签内）添加命名空间：
`xmlns:p="clr-namespace:lingualink_client.Properties"`

然后替换硬编码的文本。

*   **MainWindow.xaml**:
    ```xml
    <ui:FluentWindow
        x:Class="lingualink_client.MainWindow"
        xmlns:p="clr-namespace:lingualink_client.Properties"
        Title="{x:Static p:Strings.MainWindow_Title}" ...>
        <ui:TitleBar Title="{x:Static p:Strings.MainWindow_Title}" Grid.Row="0">...</ui:TitleBar>
        <ui:NavigationViewItem Content="{x:Static p:Strings.NavigationViewItem_Start}" TargetPageType="{x:Type pages:IndexPage}">...</ui:NavigationViewItem>
        <ui:NavigationViewItem Content="{x:Static p:Strings.NavigationViewItem_Service}" TargetPageType="{x:Type pages:ServicePage}">...</ui:NavigationViewItem>
        <ui:NavigationViewItem Content="{x:Static p:Strings.NavigationViewItem_Log}" TargetPageType="{x:Type pages:LogPage}">...</ui:NavigationViewItem>
        <ui:NavigationViewItem Content="{x:Static p:Strings.NavigationViewItem_Settings}" TargetPageType="{x:Type pages:SettingPage}">...</ui:NavigationViewItem>
    </ui:FluentWindow>
    ```

*   **Views/Pages/IndexPage.xaml**:
    ```xml
    <Page x:Class="lingualink_client.Views.IndexPage"
          xmlns:p="clr-namespace:lingualink_client.Properties" ...>
        <Label VerticalAlignment="Center" Content="{x:Static p:Strings.IndexPage_SelectMicrophoneLabel}" />
        <Button Command="{Binding RefreshMicrophonesCommand}" Content="{x:Static p:Strings.IndexPage_RefreshButtonContent}" />
        <TextBlock Text="{x:Static p:Strings.IndexPage_MicrophoneRefreshInProgress}" />
        <Expander Header="{x:Static p:Strings.IndexPage_TargetLanguagesHeader}" ...>
            <Button Command="{Binding AddLanguageCommand}" Content="{x:Static p:Strings.IndexPage_AddLanguageButtonContent}" />
            <!-- Remove button's Content is more complex due to DataTemplate, see below -->
        </Expander>
        <!-- WorkButton Content is dynamic, bound to ViewModel property -->
        <Button Content="{Binding WorkButtonContent}" .../>
        <!-- StatusText is dynamic -->
        <TextBlock Text="{Binding StatusText}" />
        <!-- TranslationResultText is dynamic -->
        <TextBlock Text="{x:Static p:Strings.IndexPage_Hint_VAD}" />
    </Page>
    ```
    对于 `ItemsControl` 内的 `Remove` 按钮 (在 `IndexPage.xaml` 的 `SelectableTargetLanguageViewModel` DataTemplate里):
    ```xml
    <Button Grid.Column="2" Margin="8,0,0,0" Padding="5,2" VerticalAlignment="Center"
            Command="{Binding RemoveCommand}" Content="{x:Static p:Strings.IndexPage_RemoveLanguageButtonContent}"
            Visibility="{Binding CanRemove, Converter={StaticResource BooleanToVisibilityConverter}}" />
    ```

*   **Views/Pages/LogPage.xaml**:
    ```xml
    <Page x:Class="lingualink_client.Views.LogPage"
          xmlns:p="clr-namespace:lingualink_client.Properties" ...>
        <Label Content="{x:Static p:Strings.LogPage_Title}" .../>
        <Button Content="{x:Static p:Strings.LogPage_ClearLogButtonContent}" Command="{Binding ClearLogCommand}" .../>
    </Page>
    ```

*   **Views/Pages/ServicePage.xaml**:
    ```xml
    <Page x:Class="lingualink_client.Views.ServicePage"
          xmlns:p="clr-namespace:lingualink_client.Properties" ...>
        <Label Grid.Row="0" Content="{x:Static p:Strings.ServicePage_ServerUrlLabel}" />
        <Expander Header="{x:Static p:Strings.ServicePage_AdvancedVADHeader}" ...>
            <Label Content="{x:Static p:Strings.ServicePage_SilenceThresholdLabel}" />
            <Label Content="{x:Static p:Strings.ServicePage_MinVoiceDurationLabel}" />
            <Label Content="{x:Static p:Strings.ServicePage_MaxVoiceDurationLabel}" />
            <Label Content="{x:Static p:Strings.ServicePage_MinRecordingVolumeLabel}" />
            <TextBlock Text="{x:Static p:Strings.ServicePage_MinRecordingVolumeHint}" .../>
        </Expander>
        <Expander Header="{x:Static p:Strings.ServicePage_OSCSettingsHeader}" ...>
            <CheckBox Content="{x:Static p:Strings.ServicePage_EnableOSCCheckbox}" ... />
            <Label Content="{x:Static p:Strings.ServicePage_OSCIPAddressLabel}" />
            <Label Content="{x:Static p:Strings.ServicePage_OSCPortLabel}" />
            <CheckBox Content="{x:Static p:Strings.ServicePage_OSCSendImmediatelyCheckbox}" ... />
            <CheckBox Content="{x:Static p:Strings.ServicePage_OSCPlayNotificationCheckbox}" ... />
        </Expander>
        <Button Command="{Binding SaveCommand}" Content="{x:Static p:Strings.ServicePage_SaveButtonContent}" ... />
        <Button Command="{Binding RevertCommand}" Content="{x:Static p:Strings.ServicePage_RevertButtonContent}" ... />
    </Page>
    ```

**第八步：修改C#代码 (ViewModels 和其他) 以使用资源**

你需要找到所有硬编码的字符串，特别是 `MessageBox.Show` 的调用，以及动态设置的文本属性（如 `StatusText`, `WorkButtonContent`）。

*   **IndexWindowViewModel.cs**:
    ```csharp
    // At the top
    using lingualink_client.Properties; // Important!

    // ...
    public IndexWindowViewModel()
    {
        // ...
        _statusText = Strings.IndexPage_Status_Initializing; // Example
        WorkButtonContent = Strings.IndexPage_WorkButton_Start; // Example
        // ...
        LogMessages.Add($"[{DateTime.Now:HH:mm:ss.fff}] {Strings.IndexPage_Status_Initializing}"); // Example for initial log
        // ...
    }

    // Inside OnGlobalSettingsChanged
    // StatusText = "状态：设置已更新。";
    // StatusText += " 请选择麦克风。";
    // StatusText += (_appSettings.EnableOsc && _oscService != null) ? " 可开始工作并发送至VRChat。" : " 可开始工作。";
    // ^^^ These need to be constructed using resource strings.

    // Inside LoadSettingsAndInitializeServices for OSC status
    // StatusText = $"状态：OSC服务已启用 ({_appSettings.OscIpAddress}:{_appSettings.OscPort})";
    // StatusText = $"状态：OSC服务初始化失败: {ex.Message}";
    // AddLogMessage($"OSC服务初始化失败: {ex.Message}"); => AddLogMessage(string.Format(Strings.Log_OSCInitFailed, ex.Message));


    // Inside ExecuteRefreshMicrophonesAsync
    // StatusText = "状态：正在刷新麦克风列表...";
    // StatusText = $"状态：刷新麦克风列表失败: {ex.Message}";
    // AddLogMessage($"刷新麦克风列表失败: {ex.Message}"); => AddLogMessage(string.Format(Strings.Log_MicRefreshFailed, ex.Message));
    // StatusText = "状态：未找到可用的麦克风设备！";
    // StatusText = "状态：麦克风列表已刷新。"; => StatusText = Strings.IndexPage_Status_MicListRefreshed;

    // Inside OnSelectedMicrophoneChanged
    // StatusText = $"状态：已选择麦克风: {_selectedMicrophone.FriendlyName}"; => StatusText = string.Format(Strings.IndexPage_Status_MicSelected, _selectedMicrophone.FriendlyName);
    // StatusText = $"状态：麦克风 '{_selectedMicrophone.FriendlyName}' 无效。";

    // Inside ExecuteToggleWorkAsync
    // MessageBox.Show("请选择一个有效的麦克风设备。", "错误", ...)
    // => MessageBox.Show(Strings.MessageBox_SelectValidMicrophone, Strings.MessageBox_Error_Title, ...)
    // WorkButtonContent = "停止工作"; => WorkButtonContent = Strings.IndexPage_WorkButton_Stop;
    // StatusText = "状态：已停止。"; => StatusText = "状态：" + Strings.IndexPage_WorkButton_Start; // Or a specific "stopped" string

    // Inside OnAudioServiceStatusUpdate
    // StatusText = $"状态：{status}"; => You might need to map 'status' from AudioService to resource keys.
    // AddLogMessage($"AudioService Status: {status}"); => AddLogMessage(string.Format(Strings.Log_AudioServiceStatus, status));

    // Inside OnAudioSegmentReadyForTranslation - this is complex, many StatusText and AddLogMessage calls
    // Each hardcoded string needs a resource key. For example:
    // currentUiStatus = $"状态：正在发送片段 ({e.TriggerReason})...";
    // => currentUiStatus = string.Format(Strings.IndexPage_Status_SendingSegment, e.TriggerReason);
    // TranslationResultText = $"错误: {errorMessage}";
    // logEntry = $"翻译错误 ({e.TriggerReason}): {errorMessage}"; => logEntry = string.Format(Strings.Log_TranslationError, e.TriggerReason, errorMessage);
    // (服务器返回成功，但无文本内容) => Needs a resource key
    // etc.

    // SaveCurrentSettings
    // AddLogMessage("目标语言设置已更新并保存。"); => AddLogMessage(Strings.Log_TargetLanguageUpdated);

    // AddLogMessage and ExecuteClearLog
    // AddLogMessage("日志已清除。"); => AddLogMessage(Strings.Log_Cleared);
    ```
    **重要**：对于 `IndexWindowViewModel` 中的 `StatusText` 和 `AddLogMessage`，因为它们经常包含动态数据 (如文件名、错误信息、时间)，你需要使用 `string.Format(Strings.YourResourceKey, dynamicValue1, dynamicValue2)`。这意味着你的资源字符串中需要有占位符，如 `{0}`、`{1}`。

*   **ServicePageViewModel.cs**:
    ```csharp
    // At the top
    using lingualink_client.Properties;

    // Inside ValidateAndBuildSettings for MessageBoxes:
    // MessageBox.Show("服务器URL无效。", "验证错误", ...)
    // => MessageBox.Show(Strings.MessageBox_InvalidServerUrl, Strings.MessageBox_Error_Title, ...)
    // ... and for all other validation messages ...

    // Inside ExecuteSaveSettings:
    // MessageBox.Show("服务相关设置已保存。", "成功", ...)
    // => MessageBox.Show(Strings.MessageBox_SettingsSaved, Strings.MessageBox_Success_Title, ...)

    // Inside ExecuteRevertSettings:
    // MessageBox.Show("更改已撤销，设置已从上次保存的状态重新加载。", "提示", ...)
    // => MessageBox.Show(Strings.MessageBox_SettingsReverted, Strings.MessageBox_Information_Title, ...)
    ```

**第九步：测试**

1.  编译并运行。默认应为中文。
2.  进入设置页面，切换语言（例如，切换到英文）。
3.  此时会弹出提示框，告知需要重启。
4.  关闭应用程序，重新启动。
5.  检查所有UI元素是否已显示为英文。
6.  对日文也进行同样测试。

**关于动态刷新UI而不是重启：**

WPF中，使用 `x:Static` 绑定的资源在加载时解析一次。要在不重启的情况下动态更改语言并让所有 `x:Static` 绑定更新，通常需要更复杂的解决方案：

*   **自定义标记扩展**：创建一个能够感知 `CurrentUICulture` 变化的标记扩展。
*   **消息传递/事件**：当语言更改时，广播一个事件，让所有ViewModel重新加载其文本属性（这意味着ViewModel中的文本属性需要实现 `INotifyPropertyChanged` 并从资源管理器获取值）。
*   **第三方库**：像 `WPFLocalizeExtension` 这样的库可以极大地简化动态语言切换。

对于当前项目，**要求用户重启是最简单且可靠的实现方式**，可以后续再考虑动态刷新。

这是一个比较大的改动，因为它涉及到项目中几乎所有的用户界面文本。请耐心逐步完成，并仔细测试每个部分。