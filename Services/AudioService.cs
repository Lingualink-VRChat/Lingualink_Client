using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
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
        private readonly double _postSpeechRecordingDurationSeconds;
        private readonly double _minVoiceDurationSeconds;
        private readonly double _maxVoiceDurationSeconds;
        private readonly double _minRecordingVolumeThreshold;

        // --- 音频增强参数 ---
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _logger;

        // --- VAD 状态变量 ---
        private bool _isSpeaking = false;
        private DateTime _lastVoiceActivityTime; // 保留用于最大时长判断
        private List<byte> _currentAudioSegment = new List<byte>();
        private WebRtcVad? _vadInstance;
        private VadState _currentState = VadState.Idle;

        // --- 追加录音状态变量 ---
        private bool _isPostRecordingActive = false;
        private DateTime _postRecordingShouldEndTime;

        private WaveInEvent? _waveSource;
        private readonly object _vadLock = new object();
        private bool _isCurrentlyWorking = false;
        private bool _isPaused = false; // 新增：暂停标志

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

        public AudioService(AppSettings settings, ILoggingManager logger)
        {
            _appSettings = settings;
            _logger = logger;
            _vadFrameSizeInBytes = (APP_SAMPLE_RATE / 1000) * (APP_BITS_PER_SAMPLE / 8) * (int)VAD_FRAME_LENGTH * APP_CHANNELS;
            _postSpeechRecordingDurationSeconds = settings.PostSpeechRecordingDurationSeconds;
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

            if (processFinalSegment && _currentAudioSegment.Count > 0)
            {
                ProcessAndSendSegment("final_on_stop", false);
            }
            else
            {
                _currentAudioSegment.Clear();
                _isSpeaking = false;
                _isPostRecordingActive = false;
            }
            
            // 更新状态为空闲
            UpdateVadState(VadState.Idle);
        }

        /// <summary>
        /// 暂停音频处理
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
            System.Diagnostics.Debug.WriteLine($"[AudioService] Paused.");
        }

        /// <summary>
        /// 恢复音频处理
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            System.Diagnostics.Debug.WriteLine($"[AudioService] Resumed.");
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
            // 在方法开头检查暂停状态
            if (_isPaused || !_isCurrentlyWorking || _vadInstance == null) return;

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
                    if (voiceDetectedThisFrame) // --- 情况1：当前帧检测到有效语音 ---
                    {
                        if (!_isSpeaking && _currentAudioSegment.Count == 0) // a) 如果之前未说话且片段为空 (刚开始说话)
                        {
                            _isSpeaking = true;
                            UpdateVadState(VadState.SpeechDetected);
                            System.Diagnostics.Debug.WriteLine($"[VAD_LOG] 语音开始");
                        }

                        // 即使之前是追加录音状态，一旦检测到有效语音，就重置追加录音状态，回到正常说话状态
                        if (_isPostRecordingActive)
                        {
                            _isPostRecordingActive = false; // 取消追加录音
                            UpdateVadState(VadState.SpeechDetected); // 重新设置为语音检测状态
                            System.Diagnostics.Debug.WriteLine($"[VAD_LOG] 追加录音期间检测到新语音，重置追加状态。");
                        }

                        _isSpeaking = true; // 确保标记为正在说话
                        _currentAudioSegment.AddRange(frameForVad); // 累积音频数据
                        _lastVoiceActivityTime = DateTime.UtcNow; // 更新最后有效语音活动时间 (主要用于最大时长判断等)

                        // 如果累积的音频超过了最大时长，则强制分割发送 (这部分逻辑依然需要)
                        if (GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _maxVoiceDurationSeconds)
                        {
                            ProcessAndSendSegment("max_duration_split", true); // 强制发送
                            // 状态已在 ProcessAndSendSegment 中处理
                        }
                    }
                    else // --- 情况2：当前帧未检测到有效语音 ---
                    {
                        if (_isSpeaking) // a) 如果之前正在说话 (这是VAD判断语音可能结束的拐点)
                        {
                            _isSpeaking = false; // 标记为不再是"活动语音"
                            _isPostRecordingActive = true; // 进入追加录音阶段
                            _postRecordingShouldEndTime = DateTime.UtcNow.AddSeconds(_postSpeechRecordingDurationSeconds);
                            // 保持 SpeechDetected 状态，不显示追加录音状态
                            System.Diagnostics.Debug.WriteLine($"[VAD_LOG] VAD判断语音结束，进入追加录音阶段，预计结束于: {_postRecordingShouldEndTime:HH:mm:ss.fff}");
                        }

                        // b) 无论是刚刚进入追加录音，还是已经在追加录音阶段，都将当前(静音)帧加入片段
                        if (_isPostRecordingActive)
                        {
                            _currentAudioSegment.AddRange(frameForVad);
                        }
                        // c) 如果之前就没在说话，也不是追加录音状态（即一直静默），则忽略此静音帧
                    }
                } // lock结束
            } // for循环结束

            // 每次处理完一整个 WaveInEventArgs 数据块后，检查是否需要发送片段
            CheckAndFinalizeSegmentIfNeeded();
        }


        /// <summary>
        /// 检查并在需要时完成片段处理 - 替代原有的 CheckSilenceTimeout
        /// </summary>
        private void CheckAndFinalizeSegmentIfNeeded()
        {
            if (!_isCurrentlyWorking) return;

            lock (_vadLock)
            {
                // 只在追加录音激活，并且时间到达时才处理
                if (_isPostRecordingActive && DateTime.UtcNow >= _postRecordingShouldEndTime)
                {
                    System.Diagnostics.Debug.WriteLine($"[VAD_LOG] 追加录音时间到达，处理并发送片段。");
                    ProcessAndSendSegment("post_recording_finished", false);
                }
                // 注意：这里没有其他基于_isSpeaking或_lastVoiceActivityTime的超时判断了
            }
        }

        /// <summary>
        /// 辅助方法：用于实际处理和发送片段的逻辑，避免重复代码
        /// </summary>
        /// <param name="triggerReason">触发发送的原因</param>
        /// <param name="isMaxDurationSplit">是否是最大时长分割</param>
        private void ProcessAndSendSegment(string triggerReason, bool isMaxDurationSplit)
        {
            var segmentData = _currentAudioSegment.ToArray();
            _currentAudioSegment.Clear();

            // 重置状态标志
            _isSpeaking = false; // 无论如何，发送后当前活动语音结束
            _isPostRecordingActive = false;
            // _postRecordingShouldEndTime 不需要立即重置，因为它会在下次进入追加录音时被重新赋值

            double segmentDurationSeconds = GetSegmentDurationSeconds(segmentData.Length);

            // 计算实际的最小录音时长要求：原始最小时长 + 追加录音时长
            double effectiveMinDuration = _minVoiceDurationSeconds + _postSpeechRecordingDurationSeconds;

            if (segmentData.Length > 0 && (isMaxDurationSplit || segmentDurationSeconds >= effectiveMinDuration))
            {
                System.Diagnostics.Debug.WriteLine($"[VAD_SEND] 触发: {triggerReason}, 时长: {segmentDurationSeconds:F2}s, 有效最小时长: {effectiveMinDuration:F2}s, 字节: {segmentData.Length}");
                // ▼▼▼ 在这里应用音频归一化处理 ▼▼▼
                segmentData = ProcessAndNormalizeAudio(segmentData);
                AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, triggerReason));
            }
            else if (segmentData.Length > 0)
            {
                StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSegmentTooShort"));
                System.Diagnostics.Debug.WriteLine($"[VAD_IGNORE] 片段过短: {triggerReason}, 时长: {segmentDurationSeconds:F2}s, 需要: {effectiveMinDuration:F2}s");
            }

            // 只要服务还在工作，发送（或忽略）片段后都应回到监听状态
            if (_isCurrentlyWorking)
            {
                UpdateVadState(VadState.Listening);
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

        /// <summary>
        /// 音频处理和归一化：RMS检测的条件增益 + 峰值归一化
        /// </summary>
        /// <param name="pcmData">PCM音频数据</param>
        /// <returns>处理后的PCM音频数据</returns>
        private byte[] ProcessAndNormalizeAudio(byte[] pcmData)
        {
            if (pcmData == null || pcmData.Length == 0)
                return pcmData ?? Array.Empty<byte>();

            // 将 byte[] 转换为 short[]
            int sampleCount = pcmData.Length / 2;
            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcmData, 0, samples, 0, pcmData.Length);

            // 第一步：RMS 计算与条件增益 (Quiet Boost)
            if (_appSettings.EnableQuietBoost)
            {
                samples = ApplyQuietBoost(samples);
            }

            // 第二步：峰值归一化 (Peak Normalization)
            if (_appSettings.EnableAudioNormalization)
            {
                samples = ApplyPeakNormalization(samples);
            }

            // 将处理后的 short[] 转换回 byte[]
            byte[] result = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, result, 0, result.Length);
            return result;
        }

        /// <summary>
        /// 应用安静语音增强
        /// </summary>
        /// <param name="samples">音频样本</param>
        /// <returns>增强后的音频样本</returns>
        private short[] ApplyQuietBoost(short[] samples)
        {
            if (samples.Length == 0) return samples;

            // 计算 RMS
            double sumOfSquares = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                double sample = samples[i];
                sumOfSquares += sample * sample;
            }
            double rms = Math.Sqrt(sumOfSquares / samples.Length);

            // 处理 RMS 为 0 的情况（纯静音）
            if (rms == 0.0)
            {
                _logger.AddMessage("Audio segment is pure silence, skipping quiet boost");
                return samples;
            }

            // 将 RMS 转换为 dBFS
            double rmsDbFs = 20.0 * Math.Log10(rms / short.MaxValue);

            // 检查是否需要应用安静增强
            if (rmsDbFs < _appSettings.QuietBoostRmsThresholdDbFs)
            {
                // 计算增益因子
                double gainFactor = Math.Pow(10.0, _appSettings.QuietBoostGainDb / 20.0);

                // 应用增益并限幅
                for (int i = 0; i < samples.Length; i++)
                {
                    double amplifiedSample = samples[i] * gainFactor;
                    samples[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, amplifiedSample));
                }

                _logger.AddMessage($"Applied quiet boost: RMS={rmsDbFs:F1}dBFS, Gain={_appSettings.QuietBoostGainDb:F1}dB");
            }

            return samples;
        }

        /// <summary>
        /// 应用峰值归一化
        /// </summary>
        /// <param name="samples">音频样本</param>
        /// <returns>归一化后的音频样本</returns>
        private short[] ApplyPeakNormalization(short[] samples)
        {
            if (samples.Length == 0) return samples;

            // 找到最大绝对值样本
            short maxAbsSample = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                short currentSampleValue = samples[i];
                short absSample;

                // 显式处理 short.MinValue 以避免 Math.Abs 溢出异常
                if (currentSampleValue == short.MinValue)
                {
                    absSample = short.MaxValue; // short.MinValue 的绝对值应该是 short.MaxValue
                }
                else
                {
                    absSample = (short)Math.Abs(currentSampleValue);
                }

                if (absSample > maxAbsSample)
                {
                    maxAbsSample = absSample;
                }
            }

            // 如果没有信号，跳过归一化
            if (maxAbsSample == 0)
            {
                _logger.AddMessage("Audio segment has no signal, skipping peak normalization");
                return samples;
            }

            // 计算目标峰值幅度（线性值）
            double targetAmplitudeLinear = Math.Pow(10.0, _appSettings.NormalizationTargetDb / 20.0) * short.MaxValue;

            // 计算缩放因子
            double scaleFactor = targetAmplitudeLinear / maxAbsSample;

            // 确保缩放因子有效（防止 NaN 或 Infinity）
            if (double.IsNaN(scaleFactor) || double.IsInfinity(scaleFactor))
            {
                _logger.AddMessage($"Warning: Invalid scaleFactor ({scaleFactor}) in peak normalization. Peak: {maxAbsSample}");
                return samples; // 返回原始样本，不进行归一化
            }

            // 应用缩放并限幅
            for (int i = 0; i < samples.Length; i++)
            {
                double scaledSample = samples[i] * scaleFactor;
                samples[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, scaledSample));
            }

            _logger.AddMessage($"Applied peak normalization: Peak={maxAbsSample}, Target={_appSettings.NormalizationTargetDb:F1}dBFS, Scale={scaleFactor:F3}");

            return samples;
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