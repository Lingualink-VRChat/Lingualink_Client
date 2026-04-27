using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using NAudio.Wave;

namespace lingualink_client.Services.Managers
{
    public sealed class PeerAudioTranslationService : IDisposable
    {
        private const string AudioCategory = "PeerAudio";

        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;
        private readonly AudioService _audioService;
        private readonly ILingualinkApiService _apiService;
        private readonly SemaphoreSlim _translationGate = new SemaphoreSlim(1, 1);
        private readonly IReadOnlyList<string> _targetBackendLanguages;

        private bool _disposed;

        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<PeerAudioTranslationResultEventArgs>? TranslationReceived;

        public bool IsWorking => _audioService.IsWorking;

        public PeerAudioTranslationService(
            AppSettings appSettings,
            ILoggingManager loggingManager,
            IEnumerable<string>? targetBackendLanguages = null)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _loggingManager = loggingManager ?? throw new ArgumentNullException(nameof(loggingManager));
            _targetBackendLanguages = targetBackendLanguages?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? Array.Empty<string>();
            _audioService = new AudioService(_appSettings, _loggingManager);
            _apiService = LingualinkApiServiceFactory.CreateApiService(_appSettings);

            _audioService.AudioSegmentReady += OnAudioSegmentReady;
            _audioService.StatusUpdated += OnAudioStatusUpdated;
        }

        public bool Start(PeerAudioCaptureTarget target)
        {
            var effectiveTarget = target ?? throw new ArgumentNullException(nameof(target));
            var success = effectiveTarget.ProcessId.HasValue
                ? _audioService.StartProcessLoopback(effectiveTarget.ProcessId.Value)
                : _audioService.StartSystemLoopback();

            if (success)
            {
                var statusKey = effectiveTarget.ProcessId.HasValue
                    ? "PeerAudioStatusListeningProcessFormat"
                    : "PeerAudioStatusListening";
                StatusUpdated?.Invoke(this, effectiveTarget.ProcessId.HasValue
                    ? string.Format(LanguageManager.GetString(statusKey), effectiveTarget.DisplayName)
                    : LanguageManager.GetString(statusKey));
            }
            else
            {
                StatusUpdated?.Invoke(this, LanguageManager.GetString("PeerAudioStatusStartFailed"));
            }

            return success;
        }

        public void Stop()
        {
            _audioService.Stop();
            StatusUpdated?.Invoke(this, LanguageManager.GetString("PeerAudioStatusStopped"));
        }

        private void OnAudioStatusUpdated(object? sender, string status)
        {
            StatusUpdated?.Invoke(this, status);
        }

        private async void OnAudioSegmentReady(object? sender, AudioSegmentEventArgs e)
        {
            if (!await _translationGate.WaitAsync(0))
            {
                _loggingManager.AddMessage("Skipped peer audio segment because previous translation is still running.", LogLevel.Warning, AudioCategory);
                return;
            }

            try
            {
                StatusUpdated?.Invoke(this, LanguageManager.GetString("PeerAudioStatusTranslating"));
                var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
                var targetLanguages = ResolveTargetLanguageCodes();
                var task = targetLanguages.Count > 0 ? "translate" : "transcribe";

                var stopwatch = Stopwatch.StartNew();
                var apiResult = await _apiService.ProcessAudioAsync(
                    e.AudioData,
                    waveFormat,
                    targetLanguages,
                    task,
                    "peer_audio_loopback");
                stopwatch.Stop();

                if (IsNoSpeechResult(apiResult))
                {
                    StatusUpdated?.Invoke(LanguageManager.GetString("PeerAudioStatusNoSpeech"));
                    return;
                }

                if (!apiResult.IsSuccess)
                {
                    var error = string.IsNullOrWhiteSpace(apiResult.ErrorMessage)
                        ? LanguageManager.GetString("PeerAudioUnknownError")
                        : apiResult.ErrorMessage;
                    StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("PeerAudioStatusFailedFormat"), error));
                    TranslationReceived?.Invoke(this, PeerAudioTranslationResultEventArgs.FromError(error));
                    return;
                }

                var displayText = BuildDisplayText(apiResult, stopwatch.Elapsed.TotalSeconds);
                TranslationReceived?.Invoke(this, new PeerAudioTranslationResultEventArgs(displayText, true, null));
                StatusUpdated?.Invoke(this, LanguageManager.GetString("PeerAudioStatusTranslated"));
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Peer audio translation failed: {ex.Message}", LogLevel.Error, AudioCategory);
                StatusUpdated?.Invoke(this, string.Format(LanguageManager.GetString("PeerAudioStatusFailedFormat"), ex.Message));
                TranslationReceived?.Invoke(this, PeerAudioTranslationResultEventArgs.FromError(ex.Message));
            }
            finally
            {
                _translationGate.Release();
            }
        }

        private List<string> ResolveTargetLanguageCodes()
        {
            var configuredLanguages = _targetBackendLanguages.Count > 0
                ? _targetBackendLanguages
                : _appSettings.PeerAudioTargetLanguages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var selectedBackendNames = configuredLanguages
                .Where(name => name != LanguageDisplayHelper.TranscriptionBackendName)
                .ToList();

            return LanguageDisplayHelper.ConvertChineseNamesToLanguageCodes(selectedBackendNames);
        }

        private static bool IsNoSpeechResult(ApiResult apiResult)
        {
            if (string.Equals(apiResult.Status, "no_speech", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !apiResult.IsSuccess
                && !string.IsNullOrWhiteSpace(apiResult.ErrorMessage)
                && apiResult.ErrorMessage.Contains("text must be a non-empty string", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDisplayText(ApiResult apiResult, double elapsedSeconds)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:HH:mm:ss}]");
            var hasContent = false;

            if (!string.IsNullOrWhiteSpace(apiResult.DisplayTranscription))
            {
                builder.AppendLine($"{LanguageManager.GetString("PeerAudioOriginalLabel")}: {apiResult.DisplayTranscription}");
                hasContent = true;
            }

            foreach (var translation in apiResult.Translations)
            {
                var languageName = LanguageDisplayHelper.ConvertLanguageCodeToChineseName(translation.Key);
                builder.AppendLine($"{languageName}: {translation.Value}");
                hasContent = true;
            }

            if (!hasContent)
            {
                builder.AppendLine(LanguageManager.GetString("PeerAudioNoTextResult"));
            }

            builder.AppendLine(string.Format(LanguageManager.GetString("PeerAudioElapsedFormat"), elapsedSeconds));
            return builder.ToString().Trim();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _audioService.AudioSegmentReady -= OnAudioSegmentReady;
            _audioService.StatusUpdated -= OnAudioStatusUpdated;
            _audioService.Dispose();
            _apiService.Dispose();
            _translationGate.Dispose();
        }
    }

    public sealed class PeerAudioTranslationResultEventArgs : EventArgs
    {
        public string DisplayText { get; }
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        public PeerAudioTranslationResultEventArgs(string displayText, bool isSuccess, string? errorMessage)
        {
            DisplayText = displayText;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static PeerAudioTranslationResultEventArgs FromError(string errorMessage)
        {
            var displayText = $"[{DateTime.Now:HH:mm:ss}]\n{LanguageManager.GetString("PeerAudioErrorLabel")}: {errorMessage}";
            return new PeerAudioTranslationResultEventArgs(displayText, false, errorMessage);
        }
    }
}
