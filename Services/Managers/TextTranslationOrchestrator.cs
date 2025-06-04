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
                // 从模板提取语言并转换为语言代码
                var templateLanguages = TemplateProcessor.ExtractLanguagesFromTemplate(_appSettings.UserCustomTemplateText);
                targetLanguageCodes = LanguageDisplayHelper.ConvertChineseNamesToLanguageCodes(templateLanguages);
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
                OnStatusUpdated("Text translation failed");
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = apiResult.ErrorMessage;
                resultArgs.ProcessedText = $"Translation error: {apiResult.ErrorMessage}";
                
                _loggingManager.AddMessage($"Text translation error: {apiResult.ErrorMessage}");
            }
            else
            {
                // 处理成功的情况
                if (!string.IsNullOrEmpty(apiResult.Transcription))
                {
                    OnStatusUpdated("Text translation successful");
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = apiResult.Transcription; // 对于文本处理，这是源文本
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;
                    
                    // 生成OSC文本 - 直接使用新API格式
                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        translatedTextForOsc = ProcessTemplateWithNewApiResult(selectedTemplate.Template, apiResult);
                        
                        if (string.IsNullOrEmpty(translatedTextForOsc))
                        {
                            _loggingManager.AddMessage("Template processing failed - contains unreplaced placeholders. Skipping OSC send for text translation");
                        }
                    }
                    else
                    {
                        // 根据选择的目标语言动态生成输出
                        translatedTextForOsc = GenerateTargetLanguageOutputFromApiResult(apiResult, targetLanguageCodes);
                    }
                    
                    resultArgs.ProcessedText = translatedTextForOsc;
                    
                    _loggingManager.AddMessage($"Text translation successful: {apiResult.Transcription} -> {apiResult.ProcessingTime}s");
                }
                else
                {
                    // 成功但没有结果文本
                    OnStatusUpdated("Text translation successful but no result");
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = "Translation successful but no result text";
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;
                    
                    _loggingManager.AddMessage($"Text translation successful but no result: {apiResult.ProcessingTime}s");
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
                OnStatusUpdated("Validating API connection...");
                var isValid = await _apiService.ValidateConnectionAsync();
                
                if (isValid)
                {
                    OnStatusUpdated("API connection validated successfully");
                    _loggingManager.AddMessage("API connection validation successful");
                }
                else
                {
                    OnStatusUpdated("API connection validation failed");
                    _loggingManager.AddMessage("API connection validation failed");
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"API connection validation error: {ex.Message}");
                _loggingManager.AddMessage($"API connection validation error: {ex.Message}");
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
            OnStatusUpdated("Sending to VRChat...");

            var oscArgs = new OscMessageEventArgs { Message = message };

            try
            {
                await _oscService!.SendChatboxMessageAsync(
                    message,
                    _appSettings.OscSendImmediately,
                    _appSettings.OscPlayNotificationSound
                );

                oscArgs.IsSuccess = true;
                OnStatusUpdated($"Sent to VRChat: {message.Split('\n')[0]}");

                _loggingManager.AddMessage($"[OSC] Sent: {message.Split('\n')[0]}");
            }
            catch (Exception ex)
            {
                oscArgs.IsSuccess = false;
                oscArgs.ErrorMessage = ex.Message;

                OnStatusUpdated($"OSC send failed: {ex.Message.Split('\n')[0]}");
                _loggingManager.AddMessage($"[OSC] Send failed: {ex.Message}");
            }

            OnOscMessageSent(oscArgs);
        }

        /// <summary>
        /// 使用新API结果处理模板
        /// </summary>
        private string ProcessTemplateWithNewApiResult(string template, ApiResult apiResult)
        {
            if (string.IsNullOrEmpty(template) || apiResult == null)
                return string.Empty;

            string result = template;

            // 替换原文占位符
            if (!string.IsNullOrEmpty(apiResult.Transcription))
            {
                result = result.Replace("{原文}", apiResult.Transcription);
                result = result.Replace("{raw_text}", apiResult.Transcription);
                result = result.Replace("{原始文本}", apiResult.Transcription);
                result = result.Replace("{完整文本}", apiResult.RawResponse ?? apiResult.Transcription);
            }

            // 替换翻译占位符 - 将语言代码转换为中文名称
            foreach (var translation in apiResult.Translations)
            {
                var chineseName = LanguageDisplayHelper.ConvertLanguageCodeToChineseName(translation.Key);
                if (!string.IsNullOrEmpty(chineseName))
                {
                    result = result.Replace($"{{{chineseName}}}", translation.Value);
                }
            }

            // 检查是否还有未替换的占位符
            var availableLanguages = new[] { "原文", "英文", "日文", "中文", "韩文", "法文", "德文", "西班牙文", "俄文", "意大利文" };
            foreach (var lang in availableLanguages)
            {
                if (result.Contains($"{{{lang}}}"))
                {
                    return string.Empty; // 包含未替换的占位符
                }
            }

            return result;
        }

        /// <summary>
        /// 根据API结果和目标语言代码生成输出文本
        /// </summary>
        private string GenerateTargetLanguageOutputFromApiResult(ApiResult apiResult, List<string> targetLanguageCodes)
        {
            if (apiResult?.Translations == null || targetLanguageCodes.Count == 0)
                return string.Empty;

            var outputParts = new List<string>();

            foreach (var languageCode in targetLanguageCodes)
            {
                if (apiResult.Translations.TryGetValue(languageCode, out var translation) && !string.IsNullOrEmpty(translation))
                {
                    outputParts.Add(translation);
                }
            }

            if (outputParts.Count == 0)
            {
                _loggingManager.AddMessage($"Warning: No translations found for target languages [{string.Join(", ", targetLanguageCodes)}], skipping OSC send");
                return string.Empty;
            }

            var result = string.Join("\n", outputParts);
            _loggingManager.AddMessage($"Generated output for languages: [{string.Join(", ", targetLanguageCodes)}] -> {outputParts.Count} translations found");

            return result;
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
