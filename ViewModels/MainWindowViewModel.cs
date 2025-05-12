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
        private OscService? _oscService; // Added OSC Service
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
        public DelegateCommand OpenSettingsCommand { get; }

        public MainWindowViewModel()
        {
            _microphoneManager = new MicrophoneManager();
            _settingsService = new SettingsService();
            
            RefreshMicrophonesCommand = new DelegateCommand(async _ => await ExecuteRefreshMicrophonesAsync(), _ => CanExecuteRefreshMicrophones());
            ToggleWorkCommand = new DelegateCommand(async _ => await ExecuteToggleWorkAsync(), _ => CanExecuteToggleWork());
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings, _ => CanExecuteOpenSettings());

            LoadSettingsAndInitializeServices(); 

            _ = ExecuteRefreshMicrophonesAsync(); 
        }

        private void LoadSettingsAndInitializeServices(bool reattachAudioEvents = false)
        {
            _appSettings = _settingsService.LoadSettings();

            // Dispose and re-initialize services that depend on settings
            if (reattachAudioEvents && _audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
            }
            _translationService?.Dispose();
            _audioService?.Dispose();
            _oscService?.Dispose(); // Dispose existing OSC service

            _translationService = new TranslationService(_appSettings.ServerUrl);
            _audioService = new AudioService(_appSettings);

            _audioService.AudioSegmentReady += OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated += OnAudioServiceStatusUpdate;

            if (_appSettings.EnableOsc)
            {
                try
                {
                    _oscService = new OscService(_appSettings.OscIpAddress, _appSettings.OscPort);
                    StatusText = $"状态：OSC服务已启用 ({_appSettings.OscIpAddress}:{_appSettings.OscPort})";
                }
                catch (Exception ex)
                {
                    _oscService = null;
                    StatusText = $"状态：OSC服务初始化失败: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"OSC Service Init Error: {ex.Message}");
                    // Optionally, show a message box or disable OSC in current session's _appSettings
                    // _appSettings.EnableOsc = false; // To prevent further attempts this session
                }
            }
            else
            {
                _oscService = null; // Ensure it's null if not enabled
            }

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
                foreach (var mic in mics) Microphones.Add(mic);

                if (Microphones.Any()) SelectedMicrophone = defaultMic ?? Microphones.First();
                else SelectedMicrophone = null;
                
                OnSelectedMicrophoneChanged(); // Update status based on selection
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
                     if (!StatusText.Contains("OSC服务")) // Preserve OSC status if it was just set
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
                    // StatusText will be updated by AudioService.StatusUpdated event
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
            OpenSettingsCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteToggleWork() => SelectedMicrophone != null && SelectedMicrophone.WaveInDeviceIndex != -1 && !IsRefreshingMicrophones;

        private void ExecuteOpenSettings(object? parameter)
        {
            var currentSettingsCopy = _settingsService.LoadSettings(); // Load fresh copy for editing
            
            var settingsWindow = new SettingsWindow(currentSettingsCopy) 
            {
                Owner = Application.Current.MainWindow 
            };

            if (settingsWindow.ShowDialog() == true) 
            {
                if (settingsWindow.UpdatedSettings != null)
                {
                    _settingsService.SaveSettings(settingsWindow.UpdatedSettings);
                    string oldStatus = StatusText; // Preserve status if it's about listening
                    LoadSettingsAndInitializeServices(true); 
                    
                    // Smart status update
                    if (oldStatus.Contains("监听") && _audioService.IsWorking) StatusText = oldStatus; // Keep listening status
                    else if (!StatusText.Contains("OSC服务初始化失败")) StatusText = "状态：设置已更新。";

                    if (!Microphones.Any() || SelectedMicrophone == null && !StatusText.Contains("OSC服务初始化失败"))
                    {
                        StatusText += " 请选择麦克风。";
                    }
                    else if (!_audioService.IsWorking && !StatusText.Contains("OSC服务初始化失败"))
                    {
                         StatusText += (_appSettings.EnableOsc && _oscService != null) ? " 可开始工作并发送至VRChat。" : " 可开始工作。";
                    }
                }
                else
                {
                    StatusText = "状态：设置保存时出现意外错误。";
                }
            }
        }
        private bool CanExecuteOpenSettings() => !_audioService.IsWorking;


        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = $"状态：{status}");
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            string currentStatus = $"状态：正在发送片段 ({e.TriggerReason})...";
            Application.Current.Dispatcher.Invoke(() => StatusText = currentStatus);

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            
            System.Diagnostics.Debug.WriteLine($"Target Languages for translation: {_appSettings.TargetLanguages}");

            var (response, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, _appSettings.TargetLanguages
            );

            string translatedTextForOsc = string.Empty; // Store the text to send via OSC

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
                        translatedTextForOsc = response.Data.Raw_Text; // Get text for OSC
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

            // OSC Sending Logic (outside of UI thread Dispatcher for the await)
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
                        StatusText = $"状态：翻译成功！但VRChat发送失败: {ex.Message.Split('\n')[0]}"; // Show first line of error
                        var sb = new StringBuilder(TranslationResultText);
                        if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine)) sb.AppendLine();
                        sb.AppendLine($"--- [OSC ERROR] Failed to send: {ex.Message} ---");
                        TranslationResultText = sb.ToString();
                    });
                }
            }

            // Reset status if still working and no critical error occurred
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
            if (_audioService != null)
            {
                _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
                _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
                _audioService.Dispose();
            }
            _translationService?.Dispose();
            _oscService?.Dispose(); // Dispose OSC Service
            GC.SuppressFinalize(this);
        }
    }
}