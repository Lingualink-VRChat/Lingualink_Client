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
        private readonly double _minRecordingVolumeThreshold;

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
            _minRecordingVolumeThreshold = settings.MinRecordingVolumeThreshold;
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
                StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusListening")); // 修改点
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("AudioStatusStartFailed"), ex.Message)); // 修改点
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
                            StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSpeechDetected")); // 修改点
                        }
                        _currentAudioSegment.AddRange(frameForVad);
                        _lastVoiceActivityTime = DateTime.UtcNow;

                        if (GetSegmentDurationSeconds(_currentAudioSegment.Count) >= _maxVoiceDurationSeconds)
                        {
                            var segmentData = _currentAudioSegment.ToArray();
                            _currentAudioSegment.Clear();
                            _lastVoiceActivityTime = DateTime.UtcNow; 
                            AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "max_duration_split"));
                            if (_isCurrentlyWorking && _isSpeaking) StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSpeechDetectedSplit")); // 修改点
                        }
                    }
                    else
                    {
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

                    StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSilenceDetectedProcess")); // 修改点

                    if (segmentData.Length > 0 && GetSegmentDurationSeconds(segmentData.Length) >= _minVoiceDurationSeconds)
                    {
                        AudioSegmentReady?.Invoke(this, new AudioSegmentEventArgs(segmentData, "silence_timeout"));
                        if (_isCurrentlyWorking && !_isSpeaking) StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusListening")); // 修改点
                    }
                    else if (segmentData.Length > 0)
                    {
                        StatusUpdated?.Invoke(this, LanguageManager.GetString("AudioStatusSegmentTooShort")); // 修改点
                    }
                }
            }
        }

        private void OnRecordingStoppedHandler(object? sender, StoppedEventArgs e)
        {
            CleanupWaveSourceResources(); 
            if (e.Exception != null)
            {
                StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("AudioStatusFatalError"), e.Exception.Message)); // 修改点
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