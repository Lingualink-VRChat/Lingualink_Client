using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using lingualink_client.Models; // Assuming Models.cs is in Models folder
using WebRtcVadSharp; // For WebRtcVadSharp types

namespace lingualink_client.Services
{
    public class AudioService : IDisposable
    {
        // --- VAD 和音频处理相关常量 ---
        private const FrameLength VAD_FRAME_LENGTH = FrameLength.Is30ms;
        private const SampleRate VAD_SAMPLE_RATE_ENUM = SampleRate.Is16kHz;
        public const int APP_SAMPLE_RATE = 16000; // Public for MainWindow to know
        public const int APP_CHANNELS = 1;
        private const int APP_BITS_PER_SAMPLE = 16;
        private readonly int _vadFrameSizeInBytes;

        private readonly double _silenceThresholdSeconds;
        private readonly double _minVoiceDurationSeconds;
        private readonly double _maxVoiceDurationSeconds;

        // --- VAD 状态变量 ---
        private bool _isSpeaking = false;
        private DateTime _lastVoiceActivityTime;
        private List<byte> _currentAudioSegment = new List<byte>();
        private WebRtcVad? _vadInstance;

        private WaveInEvent? _waveSource;
        private readonly object _vadLock = new object();
        private bool _isCurrentlyWorking = false; // Internal state

        // Event to notify when a segment is ready
        public event EventHandler<AudioSegmentEventArgs>? AudioSegmentReady;
        public event EventHandler<string>? StatusUpdated; // For simple status updates

        public AudioService(AppSettings settings)
        {
            _vadFrameSizeInBytes = (APP_SAMPLE_RATE / 1000) * (APP_BITS_PER_SAMPLE / 8) * (int)VAD_FRAME_LENGTH * APP_CHANNELS;
            _silenceThresholdSeconds = settings.SilenceThresholdSeconds;
            _minVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            _maxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
        }

        public bool IsWorking => _isCurrentlyWorking;

        public bool Start(int microphoneDeviceNumber)
        {
            if (_isCurrentlyWorking) return true; // Already working

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
                    BufferMilliseconds = (int)VAD_FRAME_LENGTH * 2, // Process 2 VAD frames at a time
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
                Stop(true); // Ensure cleanup
                return false;
            }
        }

        public void Stop(bool processFinalSegment = true)
        {
            if (!_isCurrentlyWorking && _waveSource == null && _vadInstance == null) return; // Already stopped or not started

            _isCurrentlyWorking = false; // Set first to stop processing in DataAvailable

            if (_waveSource != null)
            {
                try { _waveSource.StopRecording(); } // Triggers OnRecordingStoppedHandler
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Error stopping waveSource: {ex.Message}");
                    CleanupWaveSourceResources(); // Manual cleanup if StopRecording fails
                }
            } else {
                CleanupWaveSourceResources(); // Ensure cleanup if waveSource was somehow null
            }


            if (processFinalSegment && _currentAudioSegment.Count > 0 && GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _minVoiceDurationSeconds)
            {
                AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(_currentAudioSegment.ToArray(), "final_on_stop"));
            }
            _currentAudioSegment.Clear();
            _isSpeaking = false;
            // StatusUpdated?.Invoke(this, "已停止。"); // MainWindow will set this
        }


        private void OnVadDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isCurrentlyWorking || _vadInstance == null) return;

            for (int offset = 0; offset < e.BytesRecorded; offset += _vadFrameSizeInBytes)
            {
                int bytesToProcess = Math.Min(_vadFrameSizeInBytes, e.BytesRecorded - offset);
                if (bytesToProcess < _vadFrameSizeInBytes) break;

                byte[] frame = new byte[_vadFrameSizeInBytes];
                Buffer.BlockCopy(e.Buffer, offset, frame, 0, _vadFrameSizeInBytes);

                bool voiceDetectedThisFrame;
                try
                {
                    voiceDetectedThisFrame = _vadInstance.HasSpeech(frame);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebRtcVad.HasSpeech error: {ex.Message}");
                    voiceDetectedThisFrame = false;
                }

                lock (_vadLock)
                {
                    if (voiceDetectedThisFrame)
                    {
                        if (!_isSpeaking)
                        {
                            _isSpeaking = true;
                            StatusUpdated?.Invoke(this, "检测到语音...");
                        }
                        _currentAudioSegment.AddRange(frame);
                        _lastVoiceActivityTime = DateTime.UtcNow;

                        if (GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _maxVoiceDurationSeconds)
                        {
                            var segmentData = _currentAudioSegment.ToArray();
                            _currentAudioSegment.Clear();
                            _lastVoiceActivityTime = DateTime.UtcNow; // Reset as we just cut
                            AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "max_duration_split"));
                            if (_isCurrentlyWorking && _isSpeaking) StatusUpdated?.Invoke(this, "检测到语音 (已切分)...");
                        }
                    }
                    else // No voice
                    {
                        if (_isSpeaking)
                        {
                            // Voice just ended. Do nothing here, let CheckSilenceTimeout handle it.
                            // Optionally add this silent frame to _currentAudioSegment for smoother cuts
                            // _currentAudioSegment.AddRange(frame);
                        }
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

                    StatusUpdated?.Invoke(this, "检测到静音，准备处理...");

                    if (segmentData.Length > 0 && GetSegmentDurationSeconds(segmentData.Length) >= _minVoiceDurationSeconds)
                    {
                        AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "silence_timeout"));
                        if (_isCurrentlyWorking && !_isSpeaking) StatusUpdated?.Invoke(this, "正在监听...");
                    }
                    else if (segmentData.Length > 0)
                    {
                        StatusUpdated?.Invoke(this, "语音片段过短，已忽略。正在监听...");
                    }
                }
            }
        }

        private void OnRecordingStoppedHandler(object? sender, StoppedEventArgs e)
        {
            CleanupWaveSourceResources(); // Primary place to clean up WaveSource
            if (e.Exception != null)
            {
                StatusUpdated?.Invoke(this, $"监听时发生严重错误: {e.Exception.Message}");
                // MainWindow should handle the UI reaction to this status update (e.g., show MessageBox)
                // And potentially call Stop() on this service again to ensure full UI reset.
                _isCurrentlyWorking = false; // Ensure state is consistent
            }
        }
        
        private void CleanupWaveSourceResources()
        {
            if (_waveSource != null)
            {
                _waveSource.DataAvailable -= OnVadDataAvailable;
                _waveSource.RecordingStopped -= OnRecordingStoppedHandler;
                try { _waveSource.Dispose(); } catch { }
                _waveSource = null;
            }
        }

        private double GetSegmentDurationSeconds(int byteCount)
        {
            if (APP_SAMPLE_RATE == 0 || APP_CHANNELS == 0) return 0;
            return (double)byteCount / (APP_SAMPLE_RATE * (APP_BITS_PER_SAMPLE / 8) * APP_CHANNELS);
        }

        public void Dispose()
        {
            Stop(false); // Stop without processing final segment, ensures resources are freed
            _vadInstance?.Dispose();
            _vadInstance = null;
            GC.SuppressFinalize(this);
        }
    }
}