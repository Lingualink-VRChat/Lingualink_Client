using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using lingualink_client.Models; 
using WebRtcVadSharp; 

namespace lingualink_client.Services
{
    /// <summary>
    /// VAD状态枚举 - 用于数据驱动的状态管理
    /// </summary>
    public enum VadState
    {
        Idle,           // 空闲状态（未启动）
        Listening,      // 监听中（等待语音）
        SpeechDetected, // 检测到语音（累积中）
        Processing      // 处理中（发送后）
    }

    public class AudioService : IDisposable
    {
        // --- VAD 和音频处理相关常量 ---
        private const FrameLength VAD_FRAME_LENGTH = FrameLength.Is30ms;
        private const SampleRate VAD_SAMPLE_RATE_ENUM = SampleRate.Is16kHz;
        public const int APP_SAMPLE_RATE = 16000; 
        public const int APP_CHANNELS = 1;
        private const int APP_BITS_PER_SAMPLE = 16;
        private readonly int _vadFrameSizeInBytes;

        // --- VAD 参数 ---
        private readonly double _silenceThresholdSeconds;
        private readonly double _minVoiceDurationSeconds;
        private readonly double _maxVoiceDurationSeconds;
        private readonly double _minRecordingVolumeThreshold;

        // --- VAD 状态变量 ---
        private bool _isSpeaking = false;
        private DateTime _lastVoiceActivityTime;
        private List<byte> _currentAudioSegment = new List<byte>();
        private WebRtcVad? _vadInstance;
        private VadState _currentState = VadState.Idle;

        private WaveInEvent? _waveSource;
        private readonly object _vadLock = new object();
        private bool _isCurrentlyWorking = false; 

        public event EventHandler<AudioSegmentEventArgs>? AudioSegmentReady;
        public event EventHandler<string>? StatusUpdated;
        
        /// <summary>
        /// VAD状态变化事件（用于数据驱动绑定）
        /// </summary>
        public event EventHandler<VadState>? StateChanged;

        /// <summary>
        /// 当前VAD状态（用于数据驱动绑定）
        /// </summary>
        public VadState CurrentState => _currentState;

        public AudioService(AppSettings settings)
        {
            _vadFrameSizeInBytes = (APP_SAMPLE_RATE / 1000) * (APP_BITS_PER_SAMPLE / 8) * (int)VAD_FRAME_LENGTH * APP_CHANNELS;
            _silenceThresholdSeconds = settings.SilenceThresholdSeconds;
            _minVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            _maxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
            _minRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold;
        }

        /// <summary>
        /// 更新VAD状态并触发相应的状态事件
        /// </summary>
        private void UpdateVadState(VadState newState, bool forceUpdate = false)
        {
            if (_currentState != newState || forceUpdate)
            {
                var oldState = _currentState;
                _currentState = newState;
                
                // 触发状态变化事件（用于数据驱动绑定）
                StateChanged?.Invoke(this, newState);
                
                string statusMessage = newState switch
                {
                    VadState.Idle => string.Empty,
                    VadState.Listening => LanguageManager.GetString("AudioStatusListening"),
                    VadState.SpeechDetected => LanguageManager.GetString("AudioStatusSpeechDetected"),
                    VadState.Processing => LanguageManager.GetString("AudioStatusSilenceDetectedProcess"),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    StatusUpdated?.Invoke(this, statusMessage);
                }
                
                System.Diagnostics.Debug.WriteLine($"VAD State: {oldState} -> {newState}");
            }
        }

        public bool IsWorking => _isCurrentlyWorking;

        public bool Start(int microphoneDeviceNumber)
        {
            if (_isCurrentlyWorking) return true; 

            _isSpeaking = false;
            _currentAudioSegment.Clear();

            try
            {
                _vadInstance = new WebRtcVad
                {
                    OperatingMode = OperatingMode.Aggressive,
                    FrameLength = VAD_FRAME_LENGTH,
                    SampleRate = VAD_SAMPLE_RATE_ENUM
                };

                _waveSource = new WaveInEvent
                {
                    DeviceNumber = microphoneDeviceNumber,
                    BufferMilliseconds = (int)VAD_FRAME_LENGTH * 2, 
                    WaveFormat = new WaveFormat(APP_SAMPLE_RATE, APP_BITS_PER_SAMPLE, APP_CHANNELS)
                };
                _waveSource.DataAvailable += OnVadDataAvailable;
                _waveSource.RecordingStopped += OnRecordingStoppedHandler;
                _waveSource.StartRecording();
                _isCurrentlyWorking = true;
                
                // 使用新的状态管理系统
                UpdateVadState(VadState.Listening);
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("AudioStatusStartFailed"), ex.Message));
                System.Diagnostics.Debug.WriteLine($"AudioService Start Error: {ex.Message}");
                Stop(true); 
                return false;
            }
        }

        public void Stop(bool processFinalSegment = true)
        {
            if (!_isCurrentlyWorking && _waveSource == null && _vadInstance == null) return; 

            _isCurrentlyWorking = false; 

            if (_waveSource != null)
            {
                try { _waveSource.StopRecording(); } 
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Error stopping waveSource: {ex.Message}");
                    CleanupWaveSourceResources(); 
                }
            } else {
                CleanupWaveSourceResources(); 
            }

            if (processFinalSegment && _currentAudioSegment.Count > 0 && GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _minVoiceDurationSeconds)
            {
                AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(_currentAudioSegment.ToArray(), "final_on_stop"));
            }
            _currentAudioSegment.Clear();
            _isSpeaking = false;
            
            // 更新状态为空闲
            UpdateVadState(VadState.Idle);
        }

        private double CalculatePeakVolume(byte[] pcm16BitAudioFrame, int bytesToProcess)
        {
            if (pcm16BitAudioFrame == null || bytesToProcess == 0)
                return 0.0;

            short maxSampleMagnitude = 0;

            for (int i = 0; i < bytesToProcess; i += 2)
            {
                if (i + 1 < bytesToProcess)
                {
                    short sample = BitConverter.ToInt16(pcm16BitAudioFrame, i);
                    short currentMagnitude;

                    if (sample == short.MinValue)
                    {
                        currentMagnitude = short.MaxValue;
                    }
                    else
                    {
                        currentMagnitude = Math.Abs(sample);
                    }

                    if (currentMagnitude > maxSampleMagnitude)
                    {
                        maxSampleMagnitude = currentMagnitude;
                    }
                }
            }
            return (double)maxSampleMagnitude / short.MaxValue;
        }


        private void OnVadDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isCurrentlyWorking || _vadInstance == null) return;

            for (int offset = 0; offset < e.BytesRecorded; offset += _vadFrameSizeInBytes)
            {
                int bytesInCurrentVadFrame = Math.Min(_vadFrameSizeInBytes, e.BytesRecorded - offset);
                if (bytesInCurrentVadFrame < _vadFrameSizeInBytes && bytesInCurrentVadFrame > 0)
                {
                    break; 
                }
                if (bytesInCurrentVadFrame == 0) break;


                byte[] frameForVad = new byte[_vadFrameSizeInBytes];
                Buffer.BlockCopy(e.Buffer, offset, frameForVad, 0, _vadFrameSizeInBytes);

                bool voiceDetectedThisFrame;

                double peakVolume = CalculatePeakVolume(frameForVad, _vadFrameSizeInBytes);

                if (peakVolume < _minRecordingVolumeThreshold && _minRecordingVolumeThreshold > 0)
                {
                    voiceDetectedThisFrame = false;
                }
                else
                {
                    try
                    {
                        voiceDetectedThisFrame = _vadInstance.HasSpeech(frameForVad);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebRtcVad.HasSpeech error: {ex.Message}");
                        voiceDetectedThisFrame = false;
                    }
                }

                lock (_vadLock)
                {
                    if (voiceDetectedThisFrame)
                    {
                        if (!_isSpeaking)
                        {
                            _isSpeaking = true;
                            // 状态转换：监听 -> 检测到语音
                            UpdateVadState(VadState.SpeechDetected);
                        }
                        _currentAudioSegment.AddRange(frameForVad);
                        _lastVoiceActivityTime = DateTime.UtcNow;

                        if (GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _maxVoiceDurationSeconds)
                        {
                            var segmentData = _currentAudioSegment.ToArray();
                            _currentAudioSegment.Clear();
                            
                            // 先显示分割状态
                            StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSpeechDetectedSplit"));
                            
                            // 发送段数据
                            AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "max_duration_split"));
                            
                            // 重置状态为监听，准备检测新段
                            _isSpeaking = false;
                            _lastVoiceActivityTime = DateTime.UtcNow;
                            UpdateVadState(VadState.Listening);
                        }
                    }
                    else
                    {
                        // 静音帧，不需要立即处理，由CheckSilenceTimeout处理
                    }
                }
            }
            CheckSilenceTimeout();
        }


        private void CheckSilenceTimeout()
        {
            if (!_isCurrentlyWorking) return;
            lock (_vadLock)
            {
                if (_isSpeaking && (DateTime.UtcNow - _lastVoiceActivityTime).TotalSeconds >= _silenceThresholdSeconds)
                {
                    _isSpeaking = false;
                    var segmentData = _currentAudioSegment.ToArray();
                    _currentAudioSegment.Clear();

                    // 状态转换：检测到语音 -> 处理中
                    UpdateVadState(VadState.Processing);

                    if (segmentData.Length > 0 && GetSegmentDurationSeconds(segmentData.Length) >= _minVoiceDurationSeconds)
                    {
                        AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "silence_timeout"));
                        
                        // 段处理完成后，状态转换：处理中 -> 监听
                        if (_isCurrentlyWorking)
                        {
                            UpdateVadState(VadState.Listening);
                        }
                    }
                    else if (segmentData.Length > 0)
                    {
                        StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSegmentTooShort"));
                        
                        // 段太短也返回监听状态
                        if (_isCurrentlyWorking)
                        {
                            UpdateVadState(VadState.Listening);
                        }
                    }
                }
            }
        }

        private void OnRecordingStoppedHandler(object? sender, StoppedEventArgs e)
        {
            CleanupWaveSourceResources(); 
            if (e.Exception != null)
            {
                StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("AudioStatusFatalError"), e.Exception.Message));
                _isCurrentlyWorking = false;
                UpdateVadState(VadState.Idle);
            }
        }
        
        private void CleanupWaveSourceResources()
        {
            if (_waveSource != null)
            {
                _waveSource.DataAvailable -= OnVadDataAvailable;
                _waveSource.RecordingStopped -= OnRecordingStoppedHandler;
                try { _waveSource.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error disposing waveSource: {ex.Message}"); }
                _waveSource = null;
            }
        }

        private double GetSegmentDurationSeconds(int byteCount)
        {
            return (double)byteCount / (APP_SAMPLE_RATE * (APP_BITS_PER_SAMPLE / 8) * APP_CHANNELS);
        }

        public void Dispose()
        {
            Stop(false); 
            _vadInstance?.Dispose();
            _vadInstance = null;
            GC.SuppressFinalize(this);
        }
    }
}