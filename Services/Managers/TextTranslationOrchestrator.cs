using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 文本翻译协调器 - 负责协调文本翻译和OSC发送的完整流程
    /// </summary>
    public class TextTranslationOrchestrator : IDisposable
    {
        private readonly ILingualinkApiService _apiService;
        private readonly OscService? _oscService;
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;

        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<TranslationResultEventArgs>? TranslationCompleted;
        public event EventHandler<OscMessageEventArgs>? OscMessageSent;

        public TextTranslationOrchestrator(
            AppSettings appSettings,
            ILoggingManager loggingManager)
        {
            _appSettings = appSettings;
            _loggingManager = loggingManager;

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
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscInitFailed"), ex.Message));
                }
            }
        }

        /// <summary>
        /// 处理文本翻译
        /// </summary>
        /// <param name="text">要翻译的文本</param>
        /// <param name="sourceLanguage">源语言代码（可选）</param>
        /// <returns>处理结果</returns>
        public async Task<bool> ProcessTextAsync(string text, string? sourceLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                OnStatusUpdated("Text is required");
                return false;
            }

            OnStatusUpdated("Processing text translation...");
            _loggingManager.AddMessage($"Processing text translation: {text.Substring(0, Math.Min(text.Length, 50))}...");

            // 确定目标语言（直接使用语言代码）
            List<string> targetLanguageCodes;
            if (_appSettings.UseCustomTemplate)
            {
                // 直接从模板中提取前3个语言代码用于API请求
                targetLanguageCodes = TemplateProcessor.ExtractLanguagesFromTemplate(_appSettings.UserCustomTemplateText, 3);
                _loggingManager.AddMessage($"Languages extracted from template for API call: [{string.Join(", ", targetLanguageCodes)}]");
            }
            else
            {
                // 使用手动选择的目标语言，转换为语言代码
                var chineseLanguages = _appSettings.TargetLanguages.Split(',').Select(lang => lang.Trim()).ToList();
                targetLanguageCodes = LanguageDisplayHelper.ConvertChineseNamesToLanguageCodes(chineseLanguages);

                _loggingManager.AddMessage($"Target languages converted: [{string.Join(", ", chineseLanguages)}] -> [{string.Join(", ", targetLanguageCodes)}]");
            }

            // 使用新的API服务处理文本
            var apiResult = await _apiService.ProcessTextAsync(text, targetLanguageCodes, sourceLanguage);

            string translatedTextForOsc = string.Empty;
            var resultArgs = new TranslationResultEventArgs
            {
                TriggerReason = "manual_text"
            };

            // 记录原始响应
            if (!string.IsNullOrEmpty(apiResult.RawResponse))
            {
                _loggingManager.AddMessage($"Server raw response: {apiResult.RawResponse}");
            }

            if (!apiResult.IsSuccess)
            {
                OnStatusUpdated(LanguageManager.GetString("StatusTranslationFailed"));
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = apiResult.ErrorMessage;
                // [修复] 失败时不在VRChat输出框显示错误消息，保持为空
                resultArgs.ProcessedText = string.Empty;

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationError"), "manual_text", apiResult.ErrorMessage));
            }
            else
            {
                // 处理成功的情况
                if (!string.IsNullOrEmpty(apiResult.Transcription))
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusTranslationSuccess"));
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = apiResult.Transcription; // 对于文本处理，这是源文本
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;
                    
                    // 生成OSC文本 - 直接使用新API格式
                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        translatedTextForOsc = ApiResultProcessor.ProcessTemplate(selectedTemplate.Template, apiResult);

                        if (string.IsNullOrEmpty(translatedTextForOsc))
                        {
                            _loggingManager.AddMessage("Template processing failed - contains unreplaced placeholders. Skipping OSC send for text translation");
                        }
                    }
                    else
                    {
                        // 根据选择的目标语言动态生成输出
                        translatedTextForOsc = ApiResultProcessor.GenerateTargetLanguageOutput(apiResult, targetLanguageCodes, _loggingManager);
                    }
                    
                    resultArgs.ProcessedText = translatedTextForOsc;

                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccess"), "manual_text", apiResult.Transcription, apiResult.ProcessingTime));
                }
                else
                {
                    // 成功但没有结果文本
                    OnStatusUpdated(LanguageManager.GetString("StatusTranslationSuccessNoText"));
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;

                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"), "manual_text", apiResult.ProcessingTime));
                }
            }
            
            // 触发翻译完成事件
            OnTranslationCompleted(resultArgs);

            // 发送OSC消息
            if (_appSettings.EnableOsc && _oscService != null && !string.IsNullOrEmpty(translatedTextForOsc))
            {
                await SendOscMessageAsync(translatedTextForOsc);
            }

            return resultArgs.IsSuccess;
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
                    _loggingManager.AddMessage(LanguageManager.GetString("LogConnectionValidated"));
                }
                else
                {
                    OnStatusUpdated(LanguageManager.GetString("StatusConnectionValidationFailed"));
                    _loggingManager.AddMessage(LanguageManager.GetString("LogConnectionValidationFailed"));
                }

                return isValid;
            }
            catch (Exception ex)
            {
                OnStatusUpdated(string.Format(LanguageManager.GetString("StatusConnectionValidationError"), ex.Message));
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogConnectionValidationError"), ex.Message));
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
                _loggingManager.AddMessage($"Failed to get system capabilities: {ex.Message}");
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
                _loggingManager.AddMessage($"Failed to get supported languages: {ex.Message}");
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

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSent"), message.Split('\n')[0]));
            }
            catch (Exception ex)
            {
                oscArgs.IsSuccess = false;
                oscArgs.ErrorMessage = ex.Message;

                var errorStatus = string.Format(LanguageManager.GetString("StatusOscSendFailed"), ex.Message.Split('\n')[0]);
                OnStatusUpdated(errorStatus);

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscSendFailed"), ex.Message));
            }

            OnOscMessageSent(oscArgs);
        }



        private void OnStatusUpdated(string status)
        {
            StatusUpdated?.Invoke(this, status);
        }

        private void OnTranslationCompleted(TranslationResultEventArgs args)
        {
            TranslationCompleted?.Invoke(this, args);
        }

        private void OnOscMessageSent(OscMessageEventArgs args)
        {
            OscMessageSent?.Invoke(this, args);
        }

        public void Dispose()
        {
            _apiService?.Dispose();
            _oscService?.Dispose();
        }
    }
}
