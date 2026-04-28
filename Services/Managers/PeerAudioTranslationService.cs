using System;
using System.Collections.Generic;
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
        private readonly CancellationTokenSource _disposeCts = new();

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
                .ToList() ?? new List<string>();
            _audioService = new AudioService(_appSettings, _loggingManager);
            _apiService = LingualinkApiServiceFactory.CreateApiService(_appSettings);

            _audioService.AudioSegmentReady += OnAudioSegmentReady;
            _audioService.StatusUpdated += OnAudioStatusUpdated;
        }

        public bool Start(PeerAudioCaptureTarget target)
        {
            if (_disposed)
            {
                return false;
            }

            var effectiveTarget = target ?? throw new ArgumentNullException(nameof(target));
            var success = effectiveTarget.ProcessId.HasValue
                ? _audioService.StartProcessLoopback(effectiveTarget.ProcessId.Value)
                : _audioService.StartSystemLoopback();

            if (success)
            {
                var statusKey = effectiveTarget.ProcessId.HasValue
                    ? "PeerAudioStatusListeningProcessFormat"
                    : "PeerAudioStatusListening";
                RaiseStatus(effectiveTarget.ProcessId.HasValue
                    ? string.Format(LanguageManager.GetString(statusKey), effectiveTarget.DisplayName)
                    : LanguageManager.GetString(statusKey));
            }
            else
            {
                RaiseStatus(LanguageManager.GetString("PeerAudioStatusStartFailed"));
            }

            return success;
        }

        public void Stop()
        {
            _audioService.Stop();
            RaiseStatus(LanguageManager.GetString("PeerAudioStatusStopped"));
        }

        private void OnAudioStatusUpdated(object? sender, string status)
        {
            RaiseStatus(status);
        }

        private async void OnAudioSegmentReady(object? sender, AudioSegmentEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var enteredGate = false;
            try
            {
                enteredGate = await _translationGate.WaitAsync(0, _disposeCts.Token);
                if (!enteredGate)
                {
                    _loggingManager.AddMessage("Skipped peer audio segment because previous translation is still running.", LogLevel.Warning, AudioCategory);
                    return;
                }

                if (_disposed || _disposeCts.IsCancellationRequested)
                {
                    return;
                }

                RaiseStatus(LanguageManager.GetString("PeerAudioStatusTranslating"));
                var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
                var configuredLanguages = ResolveConfiguredBackendLanguages();
                var showOriginalText = ShouldShowOriginalText(configuredLanguages);
                var targetLanguages = ResolveTargetLanguageCodes(configuredLanguages);
                var task = targetLanguages.Count > 0 ? "translate" : "transcribe";

                var apiResult = await _apiService.ProcessAudioAsync(
                    e.AudioData,
                    waveFormat,
                    targetLanguages,
                    task,
                    "peer_audio_loopback",
                    _disposeCts.Token);

                if (IsNoSpeechResult(apiResult))
                {
                    RaiseStatus(LanguageManager.GetString("PeerAudioStatusNoSpeech"));
                    return;
                }

                if (!apiResult.IsSuccess)
                {
                    var error = string.IsNullOrWhiteSpace(apiResult.ErrorMessage)
                        ? LanguageManager.GetString("PeerAudioUnknownError")
                        : apiResult.ErrorMessage;
                    RaiseStatus(string.Format(LanguageManager.GetString("PeerAudioStatusFailedFormat"), error));
                    RaiseTranslation(PeerAudioTranslationResultEventArgs.FromError(error));
                    return;
                }

                var displayText = BuildDisplayText(apiResult, showOriginalText);
                RaiseTranslation(new PeerAudioTranslationResultEventArgs(displayText, true, null));
                RaiseStatus(LanguageManager.GetString("PeerAudioStatusTranslated"));
            }
            catch (OperationCanceledException) when (_disposed || _disposeCts.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_disposed || _disposeCts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Peer audio translation failed: {ex.Message}", LogLevel.Error, AudioCategory);
                RaiseStatus(string.Format(LanguageManager.GetString("PeerAudioStatusFailedFormat"), ex.Message));
                RaiseTranslation(PeerAudioTranslationResultEventArgs.FromError(ex.Message));
            }
            finally
            {
                if (enteredGate)
                {
                    _translationGate.Release();
                }
            }
        }

        private void RaiseStatus(string status)
        {
            if (!_disposed)
            {
                StatusUpdated?.Invoke(this, status);
            }
        }

        private void RaiseTranslation(PeerAudioTranslationResultEventArgs result)
        {
            if (!_disposed)
            {
                TranslationReceived?.Invoke(this, result);
            }
        }

        private List<string> ResolveConfiguredBackendLanguages()
        {
            IEnumerable<string> configuredLanguages = _targetBackendLanguages.Count > 0
                ? _targetBackendLanguages
                : _appSettings.PeerAudioTargetLanguages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return configuredLanguages
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ShouldShowOriginalText(IEnumerable<string> configuredLanguages)
        {
            return configuredLanguages.Any(name =>
                string.Equals(name, LanguageDisplayHelper.TranscriptionBackendName, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> ResolveTargetLanguageCodes(IEnumerable<string> configuredLanguages)
        {
            var selectedBackendNames = configuredLanguages
                .Where(name => !string.Equals(name, LanguageDisplayHelper.TranscriptionBackendName, StringComparison.OrdinalIgnoreCase))
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

        private static string BuildDisplayText(ApiResult apiResult, bool showOriginalText)
        {
            var builder = new StringBuilder();
            var hasContent = false;

            if (showOriginalText && !string.IsNullOrWhiteSpace(apiResult.DisplayTranscription))
            {
                builder.AppendLine(apiResult.DisplayTranscription);
                hasContent = true;
            }

            foreach (var translation in apiResult.Translations)
            {
                if (string.IsNullOrWhiteSpace(translation.Value))
                {
                    continue;
                }

                builder.AppendLine(translation.Value);
                hasContent = true;
            }

            if (!hasContent)
            {
                builder.AppendLine(LanguageManager.GetString("PeerAudioNoTextResult"));
            }

            return builder.ToString().Trim();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeCts.Cancel();
            _audioService.AudioSegmentReady -= OnAudioSegmentReady;
            _audioService.StatusUpdated -= OnAudioStatusUpdated;
            _audioService.Dispose();
            _apiService.Dispose();
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
            var displayText = $"{LanguageManager.GetString("PeerAudioErrorLabel")}: {errorMessage}";
            return new PeerAudioTranslationResultEventArgs(displayText, false, errorMessage);
        }
    }
}
