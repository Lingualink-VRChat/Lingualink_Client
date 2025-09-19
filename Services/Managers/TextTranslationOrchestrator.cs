using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.ViewModels.Events;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 文本翻译协调器 - 负责协调文本翻译和OSC发送的完整流程
    /// </summary>
    public class TextTranslationOrchestrator : IDisposable
    {
        private const string TextCategory = "Text";
        private const string TemplateCategory = "Template";
        private const string ApiCategory = "API";
        private const string OscCategory = "OSC";

        private readonly ILingualinkApiService _apiService;
        private readonly OscService? _oscService;
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;
        private readonly IEventAggregator _eventAggregator;

        public TextTranslationOrchestrator(
            AppSettings appSettings,
            ILoggingManager loggingManager)
        {
            _appSettings = appSettings;
            _loggingManager = loggingManager;
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();

            // 使用新的API服务工厂创建API服务
            _apiService = LingualinkApiServiceFactory.CreateApiService(_appSettings);

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
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscInitFailed"), ex.Message), LogLevel.Error, OscCategory, ex.Message);
                }
            }
        }

        /// <summary>
        /// 处理文本翻译
        /// </summary>
        /// <param name="text">要翻译的文本</param>
        /// <param name="sourceLanguage">源语言代码（可选）</param>
        /// <returns>处理结果</returns>
        public async Task<string> ProcessTextAsync(string text, string? sourceLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                OnStatusUpdated("Text is required");
                return string.Empty;
            }

            OnStatusUpdated(LanguageManager.GetString("StatusSendingText"));
            _loggingManager.AddMessage($"Processing text input: {text.Substring(0, Math.Min(text.Length, 50))}...", LogLevel.Info, TextCategory);

            // --- 智能判断任务类型和目标语言 ---
            List<string> targetLanguageCodes;
            string task = "translate";

            if (_appSettings.UseCustomTemplate)
            {
                string template = _appSettings.UserCustomTemplateText;
                targetLanguageCodes = TemplateProcessor.ExtractLanguagesFromTemplate(template, 3);
                if (targetLanguageCodes.Count == 0 && (template.Contains("{transcription}") || template.Contains("{原文}")))
                {
                    task = "transcribe";
                    _loggingManager.AddMessage("Template indicates a transcribe-only task for text input.", LogLevel.Debug, TemplateCategory);
                }
            }
            else
            {
                var selectedBackendNames = _appSettings.TargetLanguages.Split(',').Select(lang => lang.Trim()).ToList();
                var realLanguageNames = selectedBackendNames.Where(name => name != LanguageDisplayHelper.TranscriptionBackendName).ToList();
                targetLanguageCodes = LanguageDisplayHelper.ConvertChineseNamesToLanguageCodes(realLanguageNames);

                if (!targetLanguageCodes.Any() && selectedBackendNames.Contains(LanguageDisplayHelper.TranscriptionBackendName))
                {
                    task = "transcribe";
                    _loggingManager.AddMessage("Manual selection indicates a transcribe-only task for text input.", LogLevel.Debug, TextCategory);
                }
            }

            // --- 根据任务类型选择处理路径 ---
            var stopwatch = Stopwatch.StartNew();
            ApiResult apiResult;
            if (task == "transcribe")
            {
                // 本地处理 "仅转录" 任务，跳过API调用
                _loggingManager.AddMessage("Performing local transcription processing (no API call).", LogLevel.Info, TextCategory);
                apiResult = new ApiResult
                {
                    IsSuccess = true,
                    Transcription = text, // 用户的输入就是转录结果
                    Translations = new Dictionary<string, string>(),
                    ProcessingTime = 0,
                    RawResponse = text
                };
            }
            else
            {
                // 调用API进行翻译
                _loggingManager.AddMessage($"Requesting translation from API for languages: [{string.Join(", ", targetLanguageCodes)}]", LogLevel.Info, ApiCategory);
                apiResult = await _apiService.ProcessTextAsync(text, targetLanguageCodes, sourceLanguage);
            }

            stopwatch.Stop();
            var elapsedSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds);

            // --- 统一处理结果并返回最终文本 ---
            return await ProcessApiResultAndSendAsync(apiResult, "manual_text", targetLanguageCodes, task, elapsedSeconds);
        }

        /// <summary>
        /// 统一处理API结果（真实的或伪造的），生成最终文本并发送
        /// </summary>
        private async Task<string> ProcessApiResultAndSendAsync(
            ApiResult apiResult,
            string triggerReason,
            IEnumerable<string> targetLanguageCodes,
            string task,
            double elapsedSeconds)
        {

            var effectiveDuration = apiResult.ProcessingTime > 0 ? apiResult.ProcessingTime : elapsedSeconds;
            string translatedTextForOsc = string.Empty;
            var resultArgs = new TranslationResultEventArgs { TriggerReason = triggerReason, DurationSeconds = effectiveDuration };

            if (!string.IsNullOrEmpty(apiResult.RawResponse))
            {
                _loggingManager.AddMessage($"Server raw response: {apiResult.RawResponse}", LogLevel.Trace, ApiCategory);
            }

            if (!apiResult.IsSuccess)
            {
                OnStatusUpdated(LanguageManager.GetString("StatusTranslationFailed"));
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = apiResult.ErrorMessage;
                resultArgs.ProcessedText = string.Empty;
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationError"), triggerReason, apiResult.ErrorMessage), LogLevel.Error, ApiCategory, apiResult.ErrorMessage);
            }
            else
            {
                if (!string.IsNullOrEmpty(apiResult.Transcription))
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusTranslationSuccess"));
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = apiResult.Transcription;
                    resultArgs.DurationSeconds = effectiveDuration;

                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        translatedTextForOsc = ApiResultProcessor.ProcessTemplate(selectedTemplate.Template, apiResult);
                        if (string.IsNullOrEmpty(translatedTextForOsc))
                        {
                             _loggingManager.AddMessage("Template processing failed - contains unreplaced placeholders. Skipping OSC send for text translation", LogLevel.Warning, TemplateCategory);
                        }
                    }
                    else
                    {
                        var selectedBackendNamesFromSettings = _appSettings.TargetLanguages.Split(',').Select(lang => lang.Trim()).ToList();
                        translatedTextForOsc = ApiResultProcessor.GenerateTargetLanguageOutput(apiResult, selectedBackendNamesFromSettings, _loggingManager);
                    }

                    resultArgs.ProcessedText = translatedTextForOsc;
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccess"), triggerReason, apiResult.Transcription, apiResult.ProcessingTime), LogLevel.Info, ApiCategory);
                }
                else
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusTranslationSuccessNoText"));
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    resultArgs.DurationSeconds = effectiveDuration;
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"), triggerReason, apiResult.ProcessingTime), LogLevel.Info, ApiCategory);
                }
            }

            var translations = apiResult.Translations ?? new Dictionary<string, string>();

            var eventArgs = new TranslationCompletedEvent
            {
                TriggerReason = resultArgs.TriggerReason,
                OriginalText = resultArgs.OriginalText,
                ProcessedText = resultArgs.ProcessedText,
                ErrorMessage = resultArgs.ErrorMessage,
                Duration = resultArgs.DurationSeconds ?? effectiveDuration,
                IsSuccess = resultArgs.IsSuccess,
                Source = TranslationSource.Text,
                TargetLanguages = targetLanguageCodes?.ToList() ?? new List<string>(),
                Translations = new Dictionary<string, string>(translations),
                Task = task,
                RequestId = apiResult.RequestId,
                Metadata = apiResult.Metadata,
                TimestampUtc = DateTime.UtcNow
            };
            _eventAggregator.Publish(eventArgs);

            if (_appSettings.EnableOsc && _oscService != null && !string.IsNullOrEmpty(translatedTextForOsc))
            {
                await SendOscMessageAsync(translatedTextForOsc);
            }

            return translatedTextForOsc;
        }

        /// <summary>
        /// 验证API连接
        /// </summary>
        public async Task<bool> ValidateConnectionAsync()
        {
            try
            {
                OnStatusUpdated(LanguageManager.GetString("StatusValidatingConnection"));
                var isValid = await _apiService.ValidateConnectionAsync();

                if (isValid)
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusConnectionValidated"));
                    _loggingManager.AddMessage(LanguageManager.GetString("LogConnectionValidated"), LogLevel.Info, ApiCategory);
                }
                else
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusConnectionValidationFailed"));
                    _loggingManager.AddMessage(LanguageManager.GetString("LogConnectionValidationFailed"), LogLevel.Warning, ApiCategory);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                OnStatusUpdated(string.Format(LanguageManager.GetString("StatusConnectionValidationError"), ex.Message));
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogConnectionValidationError"), ex.Message), LogLevel.Error, ApiCategory, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取系统能力信息
        /// </summary>
        public async Task<SystemCapabilities?> GetCapabilitiesAsync()
        {
            try
            {
                return await _apiService.GetCapabilitiesAsync();
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to get system capabilities: {ex.Message}", LogLevel.Error, ApiCategory, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 获取支持的语言列表
        /// </summary>
        public async Task<LanguageInfo[]?> GetSupportedLanguagesAsync()
        {
            try
            {
                return await _apiService.GetSupportedLanguagesAsync();
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to get supported languages: {ex.Message}", LogLevel.Error, ApiCategory, ex.Message);
                return null;
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

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSent"), message.Split('\n')[0]), LogLevel.Info, OscCategory);
            }
            catch (Exception ex)
            {
                oscArgs.IsSuccess = false;
                oscArgs.ErrorMessage = ex.Message;

                var errorStatus = string.Format(LanguageManager.GetString("StatusOscSendFailed"), ex.Message.Split('\n')[0]);
                OnStatusUpdated(errorStatus);

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSendFailed"), ex.Message), LogLevel.Error, OscCategory, ex.Message);
            }

            _eventAggregator.Publish(new OscMessageSentEvent
            {
                Message = oscArgs.Message,
                IsSuccess = oscArgs.IsSuccess,
                ErrorMessage = oscArgs.ErrorMessage
            });
        }



        private void OnStatusUpdated(string status)
        {
            _eventAggregator.Publish(new StatusUpdatedEvent { Status = status });
        }

        public void Dispose()
        {
            _apiService?.Dispose();
            _oscService?.Dispose();
        }
    }
}
