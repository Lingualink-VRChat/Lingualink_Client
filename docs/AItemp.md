Okay, I understand. You want to move the settings UI, which is currently in a separate `SettingsWindow` dialog launched from the `IndexPage`, into the `ServicePage` so it's part of the main `NavigationView` flow. `ServicePage.xaml` already seems to have the UI structure for these settings.

Here's a plan to achieve this:

1.  **Create `ServicePageViewModel`**: This new ViewModel will take over the logic from the current `SettingsWindowViewModel`.
2.  **Adapt `SelectableTargetLanguageViewModel`**: Update it to reference `ServicePageViewModel` as its parent.
3.  **Update `ServicePage.xaml` and `ServicePage.xaml.cs`**:
    *   Set `ServicePageViewModel` as the `DataContext`.
    *   Bind the "Save" and "Cancel" (or "Revert") buttons to commands in the new ViewModel.
4.  **Implement a Notification System**: When settings are saved in `ServicePageViewModel`, `IndexWindowViewModel` (and potentially other ViewModels) needs to be notified to reload and apply these settings. We'll use a simple static event for this.
5.  **Modify `IndexWindowViewModel`**:
    *   Remove the command that opens the old `SettingsWindow`.
    *   Subscribe to the settings changed notification to refresh its services.
6.  **Update `Views/Pages/IndexPage.xaml`**: Remove the "设置" (Settings) button that launched the old dialog.
7.  **Deprecate/Delete Old Files**: Remove `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, and `SettingsWindowViewModel.cs`.
8.  **(Optional) Adjust `MainWindow.xaml`**: Consider renaming the "服务" navigation item if it's now primarily for settings.

Let's go through the changes step-by-step:

**Step 1: Create `Services/SettingsChangedNotifier.cs`**
This class will manage the static event for settings changes.

```csharp
// File: Services/SettingsChangedNotifier.cs
using System;

namespace lingualink_client.Services
{
    public static class SettingsChangedNotifier
    {
        public static event Action? SettingsChanged;

        public static void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke();
        }
    }
}
```

**Step 2: Modify `ViewModels/SelectableTargetLanguageViewModel.cs`**
Change the parent ViewModel type from `SettingsWindowViewModel` to `ServicePageViewModel`.

```csharp
// File: ViewModels/SelectableTargetLanguageViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
// lingualink_client.ViewModels; // ViewModelBase and DelegateCommand are already in this namespace

namespace lingualink_client.ViewModels
{
    public class SelectableTargetLanguageViewModel : ViewModelBase
    {
        // Changed ParentViewModel type
        public ServicePageViewModel ParentViewModel { get; }

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
        public SelectableTargetLanguageViewModel(ServicePageViewModel parent, string initialSelection, List<string> allLangsSeed)
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

**Step 3: Create `ViewModels/ServicePageViewModel.cs`**
This will be based on `ViewModels/SettingsWindowViewModel.cs`.

```csharp
// File: ViewModels/ServicePageViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services; // For SettingsService and SettingsChangedNotifier
using System; // For Uri
using System.Net; // For IPAddress

namespace lingualink_client.ViewModels
{
    public class ServicePageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings; // Holds the settings being edited/displayed

        public ObservableCollection<SelectableTargetLanguageViewModel> TargetLanguageItems { get; }
        public DelegateCommand AddLanguageCommand { get; }
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
        
        private static readonly List<string> AllSupportedLanguages = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
        };
        private const int MaxTargetLanguages = 5; 

        public ServicePageViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _currentSettings = _settingsService.LoadSettings();
            
            TargetLanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();
            AddLanguageCommand = new DelegateCommand(ExecuteAddLanguage, CanExecuteAddLanguage);
            SaveCommand = new DelegateCommand(ExecuteSaveSettings);
            RevertCommand = new DelegateCommand(ExecuteRevertSettings);

            LoadSettingsFromModel(_currentSettings);
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            TargetLanguageItems.Clear();
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
                // Pass 'this' (ServicePageViewModel instance)
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

        private bool ValidateAndBuildSettings(out AppSettings? updatedSettings)
        {
            updatedSettings = null;
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

            updatedSettings = new AppSettings
            {
                TargetLanguages = string.Join(",", selectedLangsList),
                ServerUrl = this.ServerUrl,
                SilenceThresholdSeconds = this.SilenceThresholdSeconds,
                MinVoiceDurationSeconds = this.MinVoiceDurationSeconds,
                MaxVoiceDurationSeconds = this.MaxVoiceDurationSeconds,
                MinRecordingVolumeThreshold = this.MinRecordingVolumeThreshold,
                EnableOsc = this.EnableOsc,
                OscIpAddress = this.OscIpAddress,
                OscPort = this.OscPort,
                OscSendImmediately = this.OscSendImmediately,
                OscPlayNotificationSound = this.OscPlayNotificationSound
            };
            return true;
        }

        private void ExecuteSaveSettings(object? parameter)
        {
            if (ValidateAndBuildSettings(out AppSettings? updatedSettings))
            {
                if (updatedSettings != null)
                {
                    _settingsService.SaveSettings(updatedSettings);
                    _currentSettings = updatedSettings; // Update local copy
                    SettingsChangedNotifier.RaiseSettingsChanged();
                    MessageBox.Show("设置已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ExecuteRevertSettings(object? parameter)
        {
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings);
            MessageBox.Show("更改已撤销，设置已从上次保存的状态重新加载。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
```

**Step 4: Update `Views/Pages/ServicePage.xaml.cs`**

```csharp
// File: Views/Pages/ServicePage.xaml.cs
using lingualink_client.Services; // For SettingsService
using lingualink_client.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace lingualink_client.Views
{
    public partial class ServicePage : Page
    {
        private readonly ServicePageViewModel _viewModel;

        public ServicePage()
        {
            InitializeComponent();
            // Consider using a dependency injection container for SettingsService in larger apps
            _viewModel = new ServicePageViewModel(new SettingsService());
            DataContext = _viewModel;
        }

        // Remove previous SaveButton_Click and CancelButton_Click
        // private void SaveButton_Click(object sender, RoutedEventArgs e) { }
        // private void CancelButton_Click(object sender, RoutedEventArgs e) { }
    }
}
```

**Step 5: Update `Views/Pages/ServicePage.xaml`**
Bind buttons to the new commands.

```xml
<!-- File: Views/Pages/ServicePage.xaml -->
﻿<Page
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
        <!-- Ensure BooleanToVisibilityConverter is available if not already in App.xaml or merged dicts -->
        <!-- It is in App.xaml, so this local definition is not strictly needed unless this page is used standalone -->
        <!-- <converters:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" /> -->
    </Page.Resources>
    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Label
            Grid.Row="0"
            Margin="0,0,0,2"
            Content="目标翻译语言:" />

        <ItemsControl
            Grid.Row="1"
            Margin="0,0,0,5"
            ItemsSource="{Binding TargetLanguageItems}">
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:SelectableTargetLanguageViewModel}">
                    <Grid Margin="0,3,0,3">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock
                            Grid.Column="0"
                            Margin="0,0,8,0"
                            VerticalAlignment="Center"
                            Text="{Binding Label}" />
                        <ComboBox
                            Grid.Column="1"
                            MinWidth="180"
                            MaxWidth="250"
                            HorizontalAlignment="Stretch"
                            ItemsSource="{Binding AvailableLanguages}"
                            SelectedItem="{Binding SelectedLanguage}" />
                        <Button
                            Grid.Column="2"
                            Margin="8,0,0,0"
                            Padding="5,2"
                            VerticalAlignment="Center"
                            Command="{Binding RemoveCommand}"
                            Content="移除"
                            Visibility="{Binding CanRemove, Converter={StaticResource BooleanToVisibilityConverter}}" />
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <Button
            Grid.Row="2"
            Margin="0,5,0,10"
            Padding="5,2"
            HorizontalAlignment="Left"
            Command="{Binding AddLanguageCommand}"
            Content="增加语言" />

        <Border Grid.Row="3" Height="10" />

        <Label Grid.Row="4" Content="服务器 URL:" />
        <TextBox
            x:Name="ServerUrlTextBox"
            Grid.Row="5"
            Margin="0,0,0,10"
            Text="{Binding ServerUrl, UpdateSourceTrigger=PropertyChanged}" />

        <Expander
            Grid.Row="6"
            Margin="0,0,0,10"
            Header="高级VAD设置"
            IsExpanded="False">
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
            Grid.Row="7"
            Margin="0,0,0,10"
            Header="VRChat OSC 发送设置"
            IsExpanded="{Binding EnableOsc}">
            <StackPanel Margin="10,5,0,0">
                <CheckBox
                    Margin="0,0,0,5"
                    Content="启用 OSC 发送"
                    IsChecked="{Binding EnableOsc}" />
                <Grid IsEnabled="{Binding EnableOsc}">
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
            Grid.Row="9"
            Margin="0,10,0,0"
            HorizontalAlignment="Right"
            Orientation="Horizontal">
            <Button
                Width="80"
                Margin="0,0,10,0"
                Command="{Binding SaveCommand}" <!-- Changed from Click event -->
                Content="保存"
                IsDefault="True" />
            <Button
                Width="80"
                Command="{Binding RevertCommand}" <!-- Changed from Click event -->
                Content="撤销更改" 
                IsCancel="False" /> <!-- IsCancel typically for dialogs, might not be needed -->
        </StackPanel>
    </Grid>
</Page>
```

**Step 6: Modify `ViewModels/IndexWindowViewModel.cs`**

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
// using System.Windows.Input; // No longer needed for OpenSettingsCommand
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
        private ObservableCollection<MenusBar> _menusBars;

        public ObservableCollection<MenusBar> MenusBars
        {
            get => _menusBars;
            set => SetProperty(ref _menusBars, value);
        }

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
        // Removed: public DelegateCommand OpenSettingsCommand { get; }

        public IndexWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService(); // Initialized here

            _menusBars = [
                new() { Name = "开始"},
                new() { Name = "快捷键" },
                new() { Name = "设置" }]; // This "设置" might be for the NavigationView item
            
            RefreshMicrophonesCommand = new DelegateCommand(async _ => await ExecuteRefreshMicrophonesAsync(), _ => CanExecuteRefreshMicrophones());
            ToggleWorkCommand = new DelegateCommand(async _ => await ExecuteToggleWorkAsync(), _ => CanExecuteToggleWork());
            // Removed: OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings, _ => CanExecuteOpenSettings());

            LoadSettingsAndInitializeServices(); 
            SettingsChangedNotifier.SettingsChanged += OnGlobalSettingsChanged; // Subscribe to changes

            _ = ExecuteRefreshMicrophonesAsync(); 
        }
        
        private void OnGlobalSettingsChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string oldStatus = StatusText;
                bool wasWorking = _audioService?.IsWorking ?? false;

                LoadSettingsAndInitializeServices(true);

                // Smart status update logic
                if (wasWorking && _audioService.IsWorking)
                {
                    // If it was working and is still working (e.g. mic didn't change)
                    // Keep the "listening" or "voice detected" status if appropriate.
                    // The OnAudioServiceStatusUpdate might override this, which is fine.
                    // For now, we can rely on AudioService to update its status.
                    // If AudioService was stopped by LoadSettingsAndInitializeServices, this won't apply.
                }
                else if (!StatusText.Contains("OSC服务初始化失败"))
                {
                     // Check if audio service is working now
                    if (_audioService.IsWorking) {
                         // Status will be set by AudioService
                    } else {
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
                }
                
                // Re-evaluate commands
                ToggleWorkCommand.RaiseCanExecuteChanged();
                RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            });
        }


        private void LoadSettingsAndInitializeServices(bool reattachAudioEvents = false)
        {
            bool wasWorking = _audioService?.IsWorking ?? false;
            int? previouslySelectedMicDeviceNumber = wasWorking ? SelectedMicrophone?.WaveInDeviceIndex : null;

            _appSettings = _settingsService.LoadSettings();

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
                     if(!wasWorking && !StatusText.Contains("正在刷新麦克风列表...")) // Avoid overwriting init/refresh status
                        StatusText = $"状态：OSC服务已启用 ({_appSettings.OscIpAddress}:{_appSettings.OscPort})";
                }
                catch (Exception ex)
                {
                    _oscService = null;
                    StatusText = $"状态：OSC服务初始化失败: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"OSC Service Init Error: {ex.Message}");
                }
            }
            else
            {
                _oscService = null;
            }

            // If service was working, try to restart it if mic is still valid.
            // This makes settings changes more seamless if VAD params change but mic is same.
            if (wasWorking && previouslySelectedMicDeviceNumber.HasValue && SelectedMicrophone?.WaveInDeviceIndex == previouslySelectedMicDeviceNumber)
            {
                if (_audioService.Start(previouslySelectedMicDeviceNumber.Value))
                {
                    WorkButtonContent = "停止工作";
                    IsMicrophoneComboBoxEnabled = false;
                }
                else // Start failed, update UI
                {
                    WorkButtonContent = "开始工作";
                    IsMicrophoneComboBoxEnabled = true;
                }
            } else if (wasWorking) // Was working but mic changed or invalid now, so stop
            {
                 WorkButtonContent = "开始工作";
                 IsMicrophoneComboBoxEnabled = true;
                 // StatusText might be set by AudioService if it tries to stop, or just "已停止"
            }


            RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            ToggleWorkCommand.RaiseCanExecuteChanged();
            // Removed: OpenSettingsCommand.RaiseCanExecuteChanged();
        }

        private async Task ExecuteRefreshMicrophonesAsync()
        {
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
            if (_selectedMicrophone != null)
            {
                if (_selectedMicrophone.WaveInDeviceIndex != -1 && _selectedMicrophone.WaveInDeviceIndex < WaveIn.DeviceCount)
                {
                     if (!StatusText.Contains("OSC服务") && !StatusText.Contains("设置已更新"))
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
            // Removed: OpenSettingsCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => SelectedMicrophone != null && SelectedMicrophone.WaveInDeviceIndex != -1 && !IsRefreshingMicrophones;

        // Removed ExecuteOpenSettings and CanExecuteOpenSettings methods
        // private void ExecuteOpenSettings(object? parameter) { ... }
        // private bool CanExecuteOpenSettings() => !_audioService.IsWorking;

        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = $"状态：{status}");
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            // ... (This method remains largely the same)
            string currentStatus = $"状态：正在发送片段 ({e.TriggerReason})...";
            Application.Current.Dispatcher.Invoke(() => StatusText = currentStatus);

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            
            System.Diagnostics.Debug.WriteLine($"Target Languages for translation: {_appSettings.TargetLanguages}");

            var (response, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty; 

            Application.Current.Dispatcher.Invoke(() =>
            {
                var sb = new StringBuilder(TranslationResultText);
                if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine)) sb.AppendLine(); 

                if (errorMessage != null)
                {
                    StatusText = "状态：翻译请求失败。";
                    sb.AppendLine($"翻译错误 ({e.TriggerReason}): {errorMessage}");
                }
                else if (response != null)
                {
                    if (response.Status == "success" && response.Data != null && !string.IsNullOrEmpty(response.Data.Raw_Text))
                    {
                        StatusText = "状态：翻译成功！";
                        sb.AppendLine(response.Data.Raw_Text);
                        sb.AppendLine($"(LLM处理耗时: {response.Duration_Seconds:F2}s)");
                        translatedTextForOsc = response.Data.Raw_Text; 
                    }
                    else if (response.Status == "success" && (response.Data == null || string.IsNullOrEmpty(response.Data.Raw_Text)))
                    {
                        StatusText = "状态：翻译成功，但无文本内容。";
                        sb.AppendLine("(服务器返回成功，但 raw_text 为空)");
                        sb.AppendLine($"(LLM处理耗时: {response.Duration_Seconds:F2}s)");
                    }
                    else
                    {
                        StatusText = "状态：翻译失败 (服务器)。";
                        sb.AppendLine($"服务器处理错误 ({e.TriggerReason}): {response.Message ?? "未知错误"}");
                        if(response.Details != null) sb.AppendLine($"详情: {response.Details.Content ?? "N/A"}");
                    }
                }
                else
                {
                    StatusText = "状态：收到空响应。";
                    sb.AppendLine($"收到空响应 ({e.TriggerReason})");
                }
                TranslationResultText = sb.ToString();
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
                         var sb = new StringBuilder(TranslationResultText);
                         if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine)) sb.AppendLine();
                         sb.AppendLine("--- [OSC] Message sent to VRChat ---");
                         TranslationResultText = sb.ToString();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OSC Send Error: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() => {
                        StatusText = $"状态：翻译成功！但VRChat发送失败: {ex.Message.Split('\n')[0]}";
                        var sb = new StringBuilder(TranslationResultText);
                        if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine)) sb.AppendLine();
                        sb.AppendLine($"--- [OSC ERROR] Failed to send: {ex.Message} ---");
                        TranslationResultText = sb.ToString();
                    });
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_audioService.IsWorking && !StatusText.Contains("检测到语音") && !StatusText.Contains("失败"))
                {
                    StatusText = "状态：正在监听...";
                }
            });
        }

        public void Dispose()
        {
            SettingsChangedNotifier.SettingsChanged -= OnGlobalSettingsChanged; // Unsubscribe
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

**Step 7: Update `Views/Pages/IndexPage.xaml`**
Remove the "设置" button.

```xml
<!-- File: Views/Pages/IndexPage.xaml -->
<Page
    x:Class="lingualink_client.Views.IndexPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client.Views"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    Title="IndexPage"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel
            Grid.Row="0"
            Margin="0,0,0,10"
            Orientation="Horizontal">
            <Label VerticalAlignment="Center" Content="选择麦克风：" />
            <ComboBox
                Width="300"
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
            <!-- Settings Button Removed -->
            <!-- <Button
                Margin="10,0,0,0"
                Padding="5,2"
                VerticalAlignment="Center"
                Command="{Binding OpenSettingsCommand}"
                Content="设置" /> -->
        </StackPanel>

        <Button
            Grid.Row="1"
            Padding="10,5"
            HorizontalAlignment="Left"
            Command="{Binding ToggleWorkCommand}"
            Content="{Binding WorkButtonContent}" />

        <TextBlock
            Grid.Row="2"
            Margin="0,10,0,10"
            FontSize="14"
            Text="{Binding StatusText}" />

        <TextBox
            Grid.Row="3"
            Margin="0,0,0,10"
            AcceptsReturn="True"
            IsReadOnly="True"
            Text="{Binding TranslationResultText, Mode=OneWay}"
            TextWrapping="Wrap"
            VerticalScrollBarVisibility="Auto" />

        <TextBlock
            Grid.Row="4"
            FontStyle="Italic"
            Foreground="Gray"
            Text="提示：点击“开始工作”后，应用将持续监听麦克风进行VAD检测。" />

        <StatusBar
            Grid.Row="0"
            Margin="0,0,10,0" <!-- Adjusted margin as settings button is removed -->
            HorizontalAlignment="Right"
            VerticalAlignment="Bottom"
            Opacity="0.7"
            Visibility="{Binding IsRefreshingMicrophones, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StatusBarItem>
                <TextBlock Text="正在刷新麦克风..." />
            </StatusBarItem>
        </StatusBar>
    </Grid>
</Page>
```

**Step 8: Delete old settings files**
Manually delete these files from your project:
*   `Views/SettingsWindow.xaml`
*   `Views/SettingsWindow.xaml.cs`
*   `ViewModels/SettingsWindowViewModel.cs`

**Step 9: (Optional) Review `MainWindow.xaml`**
The `NavigationViewItem` for "服务" (Service) now leads to the settings page. You might want to rename it:

```xml
<!-- In MainWindow.xaml -->
<ui:NavigationViewItem
    Content="应用设置" <!-- Or "服务与设置", "配置" etc. -->
    NavigationCacheMode="Enabled" 
    TargetPageType="{x:Type pages:ServicePage}">
    <ui:NavigationViewItem.Icon>
        <ui:SymbolIcon Symbol="Settings24" /> <!-- Changed icon to reflect settings -->
    </ui:NavigationViewItem.Icon>
</ui:NavigationViewItem>
```
And the `NavigationViewItem` in the footer for "设置" that points to `SettingPage.xaml` can either be removed if `SettingPage.xaml` is no longer needed, or `SettingPage.xaml` can be repurposed for other minor UI settings (like the language ComboBox it currently has, if that's a separate concern from the main app settings). If `ServicePage` now holds all relevant settings, you might remove the footer "设置" item or change its target.

This completes the refactoring. The application settings are now managed within `ServicePage` and `ServicePageViewModel`, and `IndexWindowViewModel` will react to saved changes. Remember to add the `Services/SettingsChangedNotifier.cs` file to your project.