using NAudio.Wave; // For WaveFormat
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using lingualink_client.Models;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly MicrophoneManager _microphoneManager;
        private AudioService _audioService;
        private TranslationService _translationService;
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

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
        public bool IsRefreshingMicrophones // For UI feedback
        {
            get => _isRefreshingMicrophones;
            set
            {
                if (SetProperty(ref _isRefreshingMicrophones, value))
                {
                    RefreshMicrophonesCommand.RaiseCanExecuteChanged();
                    // Dependent commands can also be updated here if needed
                }
            }
        }

        public DelegateCommand RefreshMicrophonesCommand { get; }
        public DelegateCommand ToggleWorkCommand { get; }
        public DelegateCommand OpenSettingsCommand { get; }

        public MainWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService();
            
            // 初始化命令
            RefreshMicrophonesCommand = new DelegateCommand(async _ => await ExecuteRefreshMicrophonesAsync(), _ => CanExecuteRefreshMicrophones());
            ToggleWorkCommand = new DelegateCommand(async _ => await ExecuteToggleWorkAsync(), _ => CanExecuteToggleWork());
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings, _ => CanExecuteOpenSettings());

            LoadSettingsAndInitializeServices(); // Initial load

            // Initial population of microphones
            // Using Task.Run to avoid blocking constructor, then updating UI context.
            // This is good practice for potentially long-running init tasks.
            _ = ExecuteRefreshMicrophonesAsync(); // Fire and forget for initial load
        }

        private void LoadSettingsAndInitializeServices(bool reattachAudioEvents = false)
        {
            _appSettings = _settingsService.LoadSettings();

            if (reattachAudioEvents && _audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
            }
            _translationService?.Dispose();
            _audioService?.Dispose();

            _translationService = new TranslationService(_appSettings.ServerUrl);
            _audioService = new AudioService(_appSettings);

            _audioService.AudioSegmentReady += OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated += OnAudioServiceStatusUpdate;

            // Update command states that might depend on settings or service states
            RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            ToggleWorkCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
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
                foreach (var mic in mics)
                {
                    Microphones.Add(mic);
                }

                if (Microphones.Any())
                {
                    SelectedMicrophone = defaultMic ?? Microphones.First();
                    OnSelectedMicrophoneChanged(); // To update status text based on new selection
                }
                else
                {
                    SelectedMicrophone = null;
                    StatusText = "状态：未找到可用的麦克风设备！";
                }
                StatusText = Microphones.Any() ? "状态：麦克风列表已刷新。" : "状态：麦克风列表已刷新，未找到设备。";
            }
            catch (Exception ex)
            {
                StatusText = $"状态：刷新麦克风列表失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"RefreshMicrophones Error: {ex.ToString()}");
            }
            finally
            {
                IsMicrophoneComboBoxEnabled = Microphones.Any();
                IsRefreshingMicrophones = false;
                ToggleWorkCommand.RaiseCanExecuteChanged(); // Selected mic might have changed
            }
        }

        private bool CanExecuteRefreshMicrophones() => !_audioService.IsWorking && !IsRefreshingMicrophones;

        private void OnSelectedMicrophoneChanged()
        {
            if (_selectedMicrophone != null)
            {
                if (_selectedMicrophone.WaveInDeviceIndex != -1 && _selectedMicrophone.WaveInDeviceIndex < WaveIn.DeviceCount)
                {
                    StatusText = $"状态：已选择麦克风: {_selectedMicrophone.FriendlyName}";
                }
                else
                {
                    // Attempt fallback for WaveInDeviceIndex if MMDevice was matched but WaveIn index was not
                    int cbIndex = Microphones.IndexOf(_selectedMicrophone);
                    if (cbIndex >= 0 && cbIndex < WaveIn.DeviceCount) {
                        _selectedMicrophone.WaveInDeviceIndex = cbIndex;
                         StatusText = $"状态：已选择麦克风 (回退索引): {_selectedMicrophone.FriendlyName}";
                    } else {
                        StatusText = $"状态：麦克风 '{_selectedMicrophone.FriendlyName}' 无效。";
                        _selectedMicrophone = null; // Invalidate selection
                    }
                }
            }
            ToggleWorkCommand.RaiseCanExecuteChanged();
        }

        private async Task ExecuteToggleWorkAsync()
        {
            ToggleWorkCommand.RaiseCanExecuteChanged(); // Disable button during operation

            if (!_audioService.IsWorking)
            {
                if (SelectedMicrophone?.WaveInDeviceIndex != -1)
                {
                    bool success = false;
                    // Starting audio service can involve device access, consider Task.Run if it ever blocks.
                    // For now, NAudio's StartRecording is generally quick.
                    await Task.Run(() => // Ensure no UI block, though StartRecording is usually fast
                    {
                        success = _audioService.Start(SelectedMicrophone.WaveInDeviceIndex);
                    });

                    if (success)
                    {
                        WorkButtonContent = "停止工作";
                        IsMicrophoneComboBoxEnabled = false;
                    }
                    else
                    {
                        StatusText = "状态：启动监听失败。"; // AudioService might provide more detail via event
                    }
                }
                else
                {
                    MessageBox.Show("请选择一个有效的麦克风设备。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                await Task.Run(() => _audioService.Stop()); // Stop can also take a moment
                WorkButtonContent = "开始工作";
                StatusText = "状态：已停止。";
                IsMicrophoneComboBoxEnabled = true;
            }

            RefreshMicrophonesCommand.RaiseCanExecuteChanged();
            ToggleWorkCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => SelectedMicrophone != null && SelectedMicrophone.WaveInDeviceIndex != -1 && !IsRefreshingMicrophones;

        private void ExecuteOpenSettings(object? parameter)
        {
            var currentSettingsCopy = _settingsService.LoadSettings();
            
            // Pass the current settings to the SettingsWindow
            var settingsWindow = new SettingsWindow(currentSettingsCopy) 
            {
                Owner = Application.Current.MainWindow 
            };

            if (settingsWindow.ShowDialog() == true) // This blocks until SettingsWindow is closed
            {
                // Retrieve the updated settings from the window's public property
                // which in turn gets it from its ViewModel
                if (settingsWindow.UpdatedSettings != null)
                {
                    _settingsService.SaveSettings(settingsWindow.UpdatedSettings);
                    LoadSettingsAndInitializeServices(true); // Pass true to reattach events
                    StatusText = "状态：设置已更新。";

                    if (!Microphones.Any() || SelectedMicrophone == null)
                    {
                        StatusText += " 请选择麦克风。";
                    }
                    else if (!_audioService.IsWorking)
                    {
                        StatusText += " 可以开始工作。";
                    }
                }
                else
                {
                    // This case should ideally not happen if DialogResult is true,
                    // but good for defensive programming.
                    StatusText = "状态：设置保存时出现意外错误。";
                }
            }
            // If ShowDialog() is false, do nothing (user cancelled)
        }
        private bool CanExecuteOpenSettings() => !_audioService.IsWorking;


        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = $"状态：{status}");
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = $"状态：正在发送片段 ({e.TriggerReason})...");

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            
            System.Diagnostics.Debug.WriteLine($"Target Languages: {_appSettings.TargetLanguages}");

            var (response, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData,
                waveFormat,
                e.TriggerReason,
                _appSettings.TargetLanguages
            );

            Application.Current.Dispatcher.Invoke(() =>
            {
                var sb = new StringBuilder(TranslationResultText);
                if (sb.Length > 0) sb.AppendLine(); // Add a newline if there's existing text

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
                        // sb.Clear(); // If you want to replace, not append
                        sb.AppendLine(response.Data.Raw_Text);
                        sb.AppendLine($"(LLM处理耗时: {response.Duration_Seconds:F2}s)");
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
                        sb.AppendLine($"详情: {response.Details?.Content ?? "N/A"}");
                    }
                }
                else
                {
                    StatusText = "状态：收到空响应。";
                    sb.AppendLine($"收到空响应 ({e.TriggerReason})");
                }
                TranslationResultText = sb.ToString();

                if (_audioService.IsWorking && !StatusText.Contains("检测到语音")) // Reset status if still working
                {
                    StatusText = "状态：正在监听...";
                }
            });
        }

        public void Dispose()
        {
            if (_audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
                _audioService.Dispose();
            }
            _translationService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}