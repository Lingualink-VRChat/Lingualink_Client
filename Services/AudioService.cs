using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using lingualink_client.Models; 
using WebRtcVadSharp; 

namespace lingualink_client.Services
{
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
        private readonly double _minRecordingVolumeThreshold; // New volume threshold

        // --- VAD 状态变量 ---
        private bool _isSpeaking = false;
        private DateTime _lastVoiceActivityTime;
        private List<byte> _currentAudioSegment = new List<byte>();
        private WebRtcVad? _vadInstance;

        private WaveInEvent? _waveSource;
        private readonly object _vadLock = new object();
        private bool _isCurrentlyWorking = false; 

        public event EventHandler<AudioSegmentEventArgs>? AudioSegmentReady;
        public event EventHandler<string>? StatusUpdated; 

        public AudioService(AppSettings settings)
        {
            _vadFrameSizeInBytes = (APP_SAMPLE_RATE / 1000) * (APP_BITS_PER_SAMPLE / 8) * (int)VAD_FRAME_LENGTH * APP_CHANNELS;
            _silenceThresholdSeconds = settings.SilenceThresholdSeconds;
            _minVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            _maxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
            _minRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold; // Store the threshold
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
                    OperatingMode = OperatingMode.Aggressive, // You might want to experiment with different modes
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
                StatusUpdated?.Invoke(this, "正在监听...");
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, $"启动监听失败: {ex.Message}");
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
        }

        private double CalculatePeakVolume(byte[] pcm16BitAudioFrame, int bytesToProcess)
        {
            if (pcm16BitAudioFrame == null || bytesToProcess == 0)
                return 0.0;

            short maxSampleMagnitude = 0; // 存储样本的最大幅度

            for (int i = 0; i < bytesToProcess; i += 2)
            {
                if (i + 1 < bytesToProcess)
                {
                    short sample = BitConverter.ToInt16(pcm16BitAudioFrame, i);
                    short currentMagnitude;

                    if (sample == short.MinValue)
                    {
                        // 特殊处理：short.MinValue 的幅度视为 short.MaxValue
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
            // 使用 short.MaxValue 进行归一化
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
                    // Partial frame at the end, typically small, VAD might not like it.
                    // For simplicity, we are requiring full VAD frames.
                    // System.Diagnostics.Debug.WriteLine($"Partial frame ({bytesInCurrentVadFrame} bytes), skipping.");
                    break; 
                }
                if (bytesInCurrentVadFrame == 0) break;


                byte[] frameForVad = new byte[_vadFrameSizeInBytes]; // VAD needs exact frame size
                // We must copy from e.Buffer with the correct offset for the current VAD frame
                Buffer.BlockCopy(e.Buffer, offset, frameForVad, 0, _vadFrameSizeInBytes);

                bool voiceDetectedThisFrame;

                // 1. Volume Check
                // Use frameForVad for volume calculation as it's the exact data VAD will see
                double peakVolume = CalculatePeakVolume(frameForVad, _vadFrameSizeInBytes);

                if (peakVolume < _minRecordingVolumeThreshold && _minRecordingVolumeThreshold > 0) // Only apply if threshold > 0
                {
                    voiceDetectedThisFrame = false; // Force silence if below volume threshold
                    // System.Diagnostics.Debug.WriteLineIf(peakVolume > 0, $"Vol: {peakVolume:F3} < Thresh: {_minRecordingVolumeThreshold:F3}. Forced silence.");
                }
                else
                {
                    // 2. VAD Check (if volume is sufficient or threshold is zero)
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
                            // Clear any short, quiet segment that might have accumulated before real speech
                            if (GetSegmentDurationSeconds(_currentAudioSegment.Count) < _minVoiceDurationSeconds * 0.5)
                            {
                                // If a tiny bit of noise got in before this loud frame, clear it.
                                // This is optional, depends on desired behavior.
                                // _currentAudioSegment.Clear(); 
                            }
                            StatusUpdated?.Invoke(this, "检测到语音...");
                        }
                        _currentAudioSegment.AddRange(frameForVad); // Add the VAD-processed frame
                        _lastVoiceActivityTime = DateTime.UtcNow;

                        if (GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _maxVoiceDurationSeconds)
                        {
                            var segmentData = _currentAudioSegment.ToArray();
                            _currentAudioSegment.Clear();
                            _lastVoiceActivityTime = DateTime.UtcNow; 
                            AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "max_duration_split"));
                            if (_isCurrentlyWorking && _isSpeaking) StatusUpdated?.Invoke(this, "检测到语音 (已切分)...");
                        }
                    }
                    else // No voice (either by VAD or volume threshold)
                    {
                        if (_isSpeaking)
                        {
                            // Voice just ended or was filtered out.
                            // Add the current (silent) frame to the segment for a small tail, if desired.
                            // This can help avoid cutting off words abruptly.
                            // _currentAudioSegment.AddRange(frameForVad); // Optional: add tail
                            // _lastVoiceActivityTime would still be the last time actual voice was detected.
                            // The CheckSilenceTimeout will handle the end of speech.
                        }
                        // If not _isSpeaking, and this frame is silent, do nothing with _currentAudioSegment.
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
                    _isSpeaking = false; // Voice has ended due to silence
                    var segmentData = _currentAudioSegment.ToArray();
                    _currentAudioSegment.Clear();

                    StatusUpdated?.Invoke(this, "检测到静音，准备处理...");

                    if (segmentData.Length > 0 && GetSegmentDurationSeconds(segmentData.Length) >= _minVoiceDurationSeconds)
                    {
                        AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "silence_timeout"));
                        if (_isCurrentlyWorking && !_isSpeaking) StatusUpdated?.Invoke(this, "正在监听...");
                    }
                    else if (segmentData.Length > 0)
                    {
                         // System.Diagnostics.Debug.WriteLine($"Segment too short ({GetSegmentDurationSeconds(segmentData.Length)}s) after silence, discarding.");
                        StatusUpdated?.Invoke(this, "语音片段过短，已忽略。正在监听...");
                    }
                    // If segmentData.Length is 0, it means only filtered noise was seen, then silence.
                }
            }
        }

        private void OnRecordingStoppedHandler(object? sender, StoppedEventArgs e)
        {
            CleanupWaveSourceResources(); 
            if (e.Exception != null)
            {
                StatusUpdated?.Invoke(this, $"监听时发生严重错误: {e.Exception.Message}");
                _isCurrentlyWorking = false; 
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
            if (APP_SAMPLE_RATE == 0 || APP_CHANNELS == 0 || APP_BITS_PER_SAMPLE == 0) return 0;
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