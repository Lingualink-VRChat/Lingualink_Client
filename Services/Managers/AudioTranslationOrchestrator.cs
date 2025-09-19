using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.ViewModels.Events;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 音频翻译协调器 - 负责协调音频处理、翻译和OSC发送的完整流程
    /// </summary>
    public class AudioTranslationOrchestrator : IAudioTranslationOrchestrator, IDisposable
    {
        private const string AudioCategory = "Audio";
        private const string OscCategory = "OSC";
        private const string TemplateCategory = "Template";
        private const string ApiCategory = "API";

        private readonly AudioService _audioService;
        private readonly ILingualinkApiService _apiService;
        private readonly OscService? _oscService;
        private bool _isTemporarilyPaused = false; // 新增：用于跟踪智能暂停状态
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;
        private readonly IEventAggregator _eventAggregator;

        public bool IsWorking => _audioService.IsWorking;

        public AudioTranslationOrchestrator(
            AppSettings appSettings,
            ILoggingManager loggingManager)
        {
            _appSettings = appSettings;
            _loggingManager = loggingManager;
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();

            // 使用新的API服务工厂创建API服务
            _apiService = LingualinkApiServiceFactory.CreateApiService(_appSettings);
            _audioService = new AudioService(_appSettings, _loggingManager);

            // 初始化OSC服务
            if (_appSettings.EnableOsc)
            {
                try
                {
                    _oscService = new OscService(_appSettings.OscIpAddress, _appSettings.OscPort);
                    OnStatusUpdated(string.Format(LanguageManager.GetString("StatusOscEnabled"), 
                        _appSettings.OscIpAddress, _appSettings.OscPort));
                }
                catch (Exception ex)
                {
                    _oscService = null;
                    OnStatusUpdated(string.Format(LanguageManager.GetString("StatusOscInitFailed"), ex.Message));
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscInitFailed"), ex.Message), LogLevel.Error, OscCategory);
                }
            }

            // 订阅音频服务事件
            _audioService.AudioSegmentReady += OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated += OnAudioServiceStatusUpdate;
            _audioService.StateChanged += OnVadStateChanged;
        }

        public bool Start(int microphoneIndex)
        {
            var success = _audioService.Start(microphoneIndex);
            if (success)
            {
                OnStatusUpdated(LanguageManager.GetString("StatusListening"));
            }
            return success;
        }

        public void Stop()
        {
            _audioService.Stop();
            OnStatusUpdated(LanguageManager.GetString("StatusStopped"));
        }

        public void Pause()
        {
            if (IsWorking && !_isTemporarilyPaused)
            {
                _audioService.Pause();
                _isTemporarilyPaused = true;
                _loggingManager.AddMessage("Audio processing paused for text input.", LogLevel.Info, AudioCategory);
                OnStatusUpdated(LanguageManager.GetString("StatusPausedForInput"));
            }
        }

        public void Resume()
        {
            if (IsWorking && _isTemporarilyPaused)
            {
                _audioService.Resume();
                _isTemporarilyPaused = false;
                _loggingManager.AddMessage("Audio processing resumed.", LogLevel.Info, AudioCategory);
                // 恢复到正常的监听状态文本
                OnStatusUpdated(LanguageManager.GetString("StatusListening"));
            }
        }

        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            // AudioService发送的status是纯状态描述，需要加上本地化的"状态:"前缀
            var formattedStatus = string.Format(LanguageManager.GetString("StatusPrefix"), status);
            OnStatusUpdated(formattedStatus);
            _loggingManager.AddMessage($"AudioService Status: {status}", LogLevel.Debug, AudioCategory);
        }

        private void OnVadStateChanged(object? sender, VadState newState)
        {
            // 发布VAD状态变化事件
            _eventAggregator.Publish(new VadStateChangedEvent
            {
                State = newState
            });
            _loggingManager.AddMessage($"VAD State Changed: {newState}", LogLevel.Debug, AudioCategory);
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            var currentUiStatus = string.Format(LanguageManager.GetString("StatusSendingSegment"), e.TriggerReason);
            OnStatusUpdated(currentUiStatus);
            _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogSendingSegment"), 
                e.TriggerReason, e.AudioData.Length, DateTime.Now), LogLevel.Info, AudioCategory);

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);

            // 确定目标语言和任务类型
            List<string> targetLanguageCodes;
            string task = "translate"; // 默认为翻译任务

            if (_appSettings.UseCustomTemplate)
            {
                string template = _appSettings.UserCustomTemplateText;
                targetLanguageCodes = TemplateProcessor.ExtractLanguagesFromTemplate(template, 3);

                // 智能判断是否为"仅转录"模式
                if (targetLanguageCodes.Count == 0)
                {
                    // 如果模板中没有目标语言占位符，但有原文占位符，则认为是"仅转录"
                    if (template.Contains("{transcription}") || template.Contains("{source_text}") || template.Contains("{原文}"))
                    {
                        task = "transcribe";
                        _loggingManager.AddMessage("Template contains only transcription placeholders. Setting task to 'transcribe'.", LogLevel.Debug, TemplateCategory);
                    }
                }
                else
                {
                     _loggingManager.AddMessage($"Languages extracted from template for API call: [{string.Join(", ", targetLanguageCodes)}]", LogLevel.Debug, TemplateCategory);
                }
            }
            else
            {
                // 手动模式下智能判断任务类型
                var selectedBackendNames = _appSettings.TargetLanguages.Split(',').Select(lang => lang.Trim()).ToList();

                // 从选择中筛选出真正的目标语言，排除我们的特殊"仅转录"选项
                var realLanguageNames = selectedBackendNames.Where(name => name != LanguageDisplayHelper.TranscriptionBackendName).ToList();
                targetLanguageCodes = LanguageDisplayHelper.ConvertChineseNamesToLanguageCodes(realLanguageNames);

                // 智能判断任务类型
                if (targetLanguageCodes.Any())
                {
                    // 如果有任何一个真正的翻译目标，任务必须是 'translate'
                    task = "translate";
                    _loggingManager.AddMessage($"Manual mode: Translation requested for languages: [{string.Join(", ", targetLanguageCodes)}]", LogLevel.Info, AudioCategory);
                }
                else if (selectedBackendNames.Contains(LanguageDisplayHelper.TranscriptionBackendName))
                {
                    // 如果没有翻译目标，但选择了"仅转录"
                    task = "transcribe";
                    _loggingManager.AddMessage("Manual mode: Only transcription selected. Setting task to 'transcribe'.", LogLevel.Debug, AudioCategory);
                }
                else
                {
                    // 兜底情况：用户可能清空了所有选项，默认为仅转录
                    task = "transcribe";
                    _loggingManager.AddMessage("Manual mode: No target languages selected. Defaulting to transcribe-only task.", LogLevel.Warning, AudioCategory);
                }
            }

            // 使用新的API服务处理音频，并传入确定的任务类型
            var stopwatch = Stopwatch.StartNew();
            var apiResult = await _apiService.ProcessAudioAsync(e.AudioData, waveFormat, targetLanguageCodes, task, e.TriggerReason);
            stopwatch.Stop();

            var elapsedSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds);
            var effectiveDuration = apiResult.ProcessingTime > 0 ? apiResult.ProcessingTime : elapsedSeconds;

            string translatedTextForOsc = string.Empty;
            var resultArgs = new TranslationResultEventArgs
            {
                TriggerReason = e.TriggerReason,
                DurationSeconds = effectiveDuration
            };

            // 记录原始响应
            if (!string.IsNullOrEmpty(apiResult.RawResponse))
            {
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogServerRawResponse"),
                    e.TriggerReason, apiResult.RawResponse), LogLevel.Trace, ApiCategory);
            }

            if (!apiResult.IsSuccess)
            {
                resultArgs.DurationSeconds = effectiveDuration;
                currentUiStatus = LanguageManager.GetString("StatusTranslationFailed");
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = apiResult.ErrorMessage;
                // [修复] 失败时不在VRChat输出框显示错误消息，保持为空
                resultArgs.ProcessedText = string.Empty;

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationError"),
                    e.TriggerReason, apiResult.ErrorMessage), LogLevel.Error, ApiCategory, apiResult.ErrorMessage);
            }
            else
            {
                // 处理成功的情况
                if (!string.IsNullOrEmpty(apiResult.Transcription))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccess");
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = apiResult.Transcription;
                    resultArgs.DurationSeconds = effectiveDuration;
                    
                    // 生成OSC文本 - 直接使用新API格式
                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        translatedTextForOsc = ApiResultProcessor.ProcessTemplate(selectedTemplate.Template, apiResult);

                        if (string.IsNullOrEmpty(translatedTextForOsc))
                        {
                            _loggingManager.AddMessage($"Template processing failed - contains unreplaced placeholders. Skipping OSC send for trigger: {e.TriggerReason}", LogLevel.Warning, TemplateCategory);
                        }
                    }
                    else
                    {
                        // 根据选择的目标语言动态生成输出
                        // [核心修复] 传递完整的用户选择，而不是只传递给API的语言代码
                        var selectedBackendNamesFromSettings = _appSettings.TargetLanguages.Split(',').Select(lang => lang.Trim()).ToList();
                        translatedTextForOsc = ApiResultProcessor.GenerateTargetLanguageOutput(apiResult, selectedBackendNamesFromSettings, _loggingManager);
                    }
                    
                    resultArgs.ProcessedText = translatedTextForOsc;
                    
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccess"),
                        e.TriggerReason, apiResult.Transcription, apiResult.ProcessingTime), LogLevel.Info, ApiCategory);
                }
                else
                {
                    // 成功但没有转录文本
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccessNoText");
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    resultArgs.DurationSeconds = effectiveDuration;

                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"),
                        e.TriggerReason, apiResult.ProcessingTime), LogLevel.Info, ApiCategory);
                }
            }
            
            // 触发翻译完成事件
            OnStatusUpdated(currentUiStatus);
            OnTranslationCompleted(resultArgs, apiResult, targetLanguageCodes, task, effectiveDuration);

            // 发送OSC消息
            if (_appSettings.EnableOsc && _oscService != null && !string.IsNullOrEmpty(translatedTextForOsc))
            {
                await SendOscMessageAsync(translatedTextForOsc);
            }

            // 如果音频服务仍在工作且没有其他重要消息，恢复到"监听中..."状态
            if (_audioService.IsWorking && 
                !currentUiStatus.Contains(LanguageManager.GetString("AudioStatusSpeechDetected").Split('.')[0]) && 
                !currentUiStatus.Contains(LanguageManager.GetString("StatusTranslationFailed").Split(':')[0]) && 
                !currentUiStatus.Contains(LanguageManager.GetString("StatusOscSendFailed").Split(':')[0]))
            {
                OnStatusUpdated(LanguageManager.GetString("StatusListening"));
            }
        }

        private async Task SendOscMessageAsync(string message)
        {
            OnStatusUpdated(LanguageManager.GetString("StatusSendingToVRChat"));
            
            var oscArgs = new OscMessageEventArgs { Message = message };
            
            try
            {
                await _oscService!.SendChatboxMessageAsync(
                    message, 
                    _appSettings.OscSendImmediately, 
                    _appSettings.OscPlayNotificationSound
                );
                
                oscArgs.IsSuccess = true;
                var successStatus = string.Format(
                    LanguageManager.GetString("StatusTranslationSuccess") + " " + 
                    LanguageManager.GetString("LogOscSent").Replace("[OSC] ", ""), 
                    message.Split('\n')[0]);
                OnStatusUpdated(successStatus);
                
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSent"), 
                    message.Split('\n')[0]), LogLevel.Info, OscCategory);
            }
            catch (Exception ex)
            {
                oscArgs.IsSuccess = false;
                oscArgs.ErrorMessage = ex.Message;
                
                var errorStatus = string.Format(LanguageManager.GetString("StatusOscSendFailed"), 
                    ex.Message.Split('\n')[0]);
                OnStatusUpdated(errorStatus);
                
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSendFailed"), ex.Message), LogLevel.Error, OscCategory, ex.Message);
            }
            
            OnOscMessageSent(oscArgs);
        }

        private void OnStatusUpdated(string status)
        {
            _eventAggregator.Publish(new StatusUpdatedEvent
            {
                Status = status
            });
        }

        private void OnTranslationCompleted(
            TranslationResultEventArgs args,
            ApiResult apiResult,
            IEnumerable<string> targetLanguageCodes,
            string task,
            double fallbackDurationSeconds)
        {
            var translations = apiResult.Translations ?? new Dictionary<string, string>();
            var durationSeconds = args.DurationSeconds.HasValue && args.DurationSeconds > 0
                ? args.DurationSeconds.Value
                : (apiResult.ProcessingTime > 0 ? apiResult.ProcessingTime : fallbackDurationSeconds);

            _eventAggregator.Publish(new TranslationCompletedEvent
            {
                TriggerReason = args.TriggerReason,
                OriginalText = args.OriginalText,
                ProcessedText = args.ProcessedText,
                ErrorMessage = args.ErrorMessage,
                Duration = durationSeconds,
                IsSuccess = args.IsSuccess,
                Source = TranslationSource.Audio,
                TargetLanguages = targetLanguageCodes?.ToList() ?? new List<string>(),
                Translations = new Dictionary<string, string>(translations),
                Task = task,
                RequestId = apiResult.RequestId,
                Metadata = apiResult.Metadata,
                TimestampUtc = DateTime.UtcNow
            });
        }

        private void OnOscMessageSent(OscMessageEventArgs args)
        {
            _eventAggregator.Publish(new OscMessageSentEvent
            {
                Message = args.Message,
                IsSuccess = args.IsSuccess,
                ErrorMessage = args.ErrorMessage
            });
        }





        public void Dispose()
        {
            _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
            _audioService.StateChanged -= OnVadStateChanged;

            _audioService?.Dispose();
            _apiService?.Dispose();
            _oscService?.Dispose();
        }
    }
} 
