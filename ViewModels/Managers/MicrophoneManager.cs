using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Events;

namespace lingualink_client.ViewModels.Managers
{
    /// <summary>
    /// 麦克风管理器实现 - 管理麦克风设备的发现、选择和状态
    /// </summary>
    public class MicrophoneManager : IMicrophoneManager, INotifyPropertyChanged
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggingManager _logger;
        private readonly Services.MicrophoneService _microphoneService;
        private MMDeviceWrapper? _selectedMicrophone;
        private bool _isRefreshing = false;
        private bool _isEnabled = true;

        public ObservableCollection<MMDeviceWrapper> Microphones { get; }

        public MMDeviceWrapper? SelectedMicrophone
        {
            get => _selectedMicrophone;
            set
            {
                if (_selectedMicrophone != value)
                {
                    var oldValue = _selectedMicrophone;
                    _selectedMicrophone = value;
                    
                    OnMicrophoneChanged(oldValue, value);
                }
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRefreshing)));

                    // 发布刷新状态变更事件
                    _eventAggregator.Publish(new MicrophoneRefreshingStateChangedEvent
                    {
                        IsRefreshing = value
                    });

                    Debug.WriteLine($"MicrophoneManager: Refreshing state changed to {value}");
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));

                    // 发布启用状态变更事件
                    _eventAggregator.Publish(new MicrophoneEnabledStateChangedEvent
                    {
                        IsEnabled = value
                    });

                    Debug.WriteLine($"MicrophoneManager: Enabled state changed to {value}");
                }
            }
        }

        public bool HasMicrophones => Microphones.Any();

        public bool IsSelectedMicrophoneValid =>
            SelectedMicrophone != null &&
            SelectedMicrophone.WaveInDeviceIndex != -1 &&
            SelectedMicrophone.WaveInDeviceIndex < WaveIn.DeviceCount;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MicrophoneManager()
        {
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();
            _logger = ServiceContainer.Resolve<ILoggingManager>();
            _microphoneService = new Services.MicrophoneService();
            
            Microphones = new ObservableCollection<MMDeviceWrapper>();
            
            Debug.WriteLine("MicrophoneManager: Initialized");
        }

        public async Task RefreshAsync()
        {
            if (IsRefreshing)
            {
                Debug.WriteLine("MicrophoneManager: Refresh already in progress, skipping");
                return;
            }

            IsRefreshing = true;
            _logger.AddMessage("Refreshing microphone list...");

            try
            {
                Debug.WriteLine("MicrophoneManager: Starting microphone refresh");

                List<MMDeviceWrapper> mics = new List<MMDeviceWrapper>();
                MMDeviceWrapper? defaultMic = null;

                await Task.Run(() =>
                {
                    mics = _microphoneService.GetAvailableMicrophones(out defaultMic);
                });

                // 更新麦克风列表
                Microphones.Clear();
                foreach (var mic in mics)
                {
                    Microphones.Add(mic);
                }

                // 选择默认麦克风
                if (Microphones.Any())
                {
                    SelectedMicrophone = defaultMic ?? Microphones.First();
                    _logger.AddMessage($"Found {Microphones.Count} microphone(s), selected: {SelectedMicrophone.FriendlyName}");
                }
                else
                {
                    SelectedMicrophone = null;
                    _logger.AddMessage("No microphone devices found");
                }

                Debug.WriteLine($"MicrophoneManager: Refresh completed. Found {Microphones.Count} microphones");

                // 发布事件
                _eventAggregator.Publish(new MicrophoneChangedEvent
                {
                    SelectedMicrophone = SelectedMicrophone,
                    IsRefreshing = false
                });
            }
            catch (Exception ex)
            {
                _logger.AddMessage($"Failed to refresh microphones: {ex.Message}");
                Debug.WriteLine($"MicrophoneManager: Refresh failed: {ex}");
                
                // 确保在异常情况下也清理状态
                SelectedMicrophone = null;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void OnMicrophoneChanged(MMDeviceWrapper? oldValue, MMDeviceWrapper? newValue)
        {
            Debug.WriteLine($"MicrophoneManager: Microphone changed from '{oldValue?.FriendlyName}' to '{newValue?.FriendlyName}'");

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMicrophone)));

            if (newValue != null)
            {
                // 验证麦克风索引
                if (newValue.WaveInDeviceIndex == -1 || newValue.WaveInDeviceIndex >= WaveIn.DeviceCount)
                {
                    // 尝试修复索引
                    int cbIndex = Microphones.IndexOf(newValue);
                    if (cbIndex >= 0 && cbIndex < WaveIn.DeviceCount)
                    {
                        newValue.WaveInDeviceIndex = cbIndex;
                        _logger.AddMessage($"Fixed microphone index for: {newValue.FriendlyName}");
                        Debug.WriteLine($"MicrophoneManager: Fixed microphone index to {cbIndex}");
                    }
                    else
                    {
                        _logger.AddMessage($"Invalid microphone: {newValue.FriendlyName}");
                        Debug.WriteLine($"MicrophoneManager: Invalid microphone, clearing selection");
                        _selectedMicrophone = null; // 直接设置，避免递归
                        newValue = null;
                    }
                }
                else
                {
                    _logger.AddMessage($"Selected microphone: {newValue.FriendlyName}");
                }
            }

            // 发布全局事件
            _eventAggregator.Publish(new MicrophoneChangedEvent
            {
                SelectedMicrophone = newValue,
                IsRefreshing = IsRefreshing
            });
        }
    }
} 
