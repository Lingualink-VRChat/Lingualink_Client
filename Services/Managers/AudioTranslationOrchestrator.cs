using System;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
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
        private readonly AudioService _audioService;
        private readonly ILingualinkApiService _apiService;
        private readonly OscService? _oscService;
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;

        public bool IsWorking => _audioService.IsWorking;

        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<TranslationResultEventArgs>? TranslationCompleted;
        public event EventHandler<OscMessageEventArgs>? OscMessageSent;
        
        /// <summary>
        /// VAD状态变化事件（用于更精确的状态管理）
        /// </summary>
        public event EventHandler<VadState>? VadStateChanged;

        public AudioTranslationOrchestrator(
            AppSettings appSettings,
            ILoggingManager loggingManager)
        {
            _appSettings = appSettings;
            _loggingManager = loggingManager;

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
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogOscInitFailed"), ex.Message));
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

        private void OnAudioServiceStatusUpdate(object? sender, string status)
        {
            // AudioService发送的status是纯状态描述，需要加上本地化的"状态:"前缀
            var formattedStatus = string.Format(LanguageManager.GetString("StatusPrefix"), status);
            OnStatusUpdated(formattedStatus);
            _loggingManager.AddMessage($"AudioService Status: {status}");
        }

        private void OnVadStateChanged(object? sender, VadState newState)
        {
            // 转发VAD状态变化事件给数据驱动组件
            VadStateChanged?.Invoke(this, newState);
            _loggingManager.AddMessage($"VAD State Changed: {newState}");
        }

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            var currentUiStatus = string.Format(LanguageManager.GetString("StatusSendingSegment"), e.TriggerReason);
            OnStatusUpdated(currentUiStatus);
            _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogSendingSegment"), 
                e.TriggerReason, e.AudioData.Length, DateTime.Now));

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);

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

            // 使用新的API服务处理音频
            var apiResult = await _apiService.ProcessAudioAsync(e.AudioData, waveFormat, targetLanguageCodes, e.TriggerReason);

            string translatedTextForOsc = string.Empty;
            var resultArgs = new TranslationResultEventArgs
            {
                TriggerReason = e.TriggerReason
            };

            // 记录原始响应
            if (!string.IsNullOrEmpty(apiResult.RawResponse))
            {
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogServerRawResponse"),
                    e.TriggerReason, apiResult.RawResponse));
            }

            if (!apiResult.IsSuccess)
            {
                currentUiStatus = LanguageManager.GetString("StatusTranslationFailed");
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = apiResult.ErrorMessage;
                resultArgs.ProcessedText = string.Format(LanguageManager.GetString("TranslationError"), apiResult.ErrorMessage);

                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationError"),
                    e.TriggerReason, apiResult.ErrorMessage));
            }
            else
            {
                // 处理成功的情况
                if (!string.IsNullOrEmpty(apiResult.Transcription))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccess");
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = apiResult.Transcription;
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;
                    
                    // 生成OSC文本 - 直接使用新API格式
                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        translatedTextForOsc = ProcessTemplateWithNewApiResult(selectedTemplate.Template, apiResult);

                        if (string.IsNullOrEmpty(translatedTextForOsc))
                        {
                            _loggingManager.AddMessage($"Template processing failed - contains unreplaced placeholders. Skipping OSC send for trigger: {e.TriggerReason}");
                        }
                    }
                    else
                    {
                        // 根据选择的目标语言动态生成输出
                        translatedTextForOsc = GenerateTargetLanguageOutputFromApiResult(apiResult, targetLanguageCodes);
                    }
                    
                    resultArgs.ProcessedText = translatedTextForOsc;
                    
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccess"),
                        e.TriggerReason, apiResult.Transcription, apiResult.ProcessingTime));
                }
                else
                {
                    // 成功但没有转录文本
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccessNoText");
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    resultArgs.DurationSeconds = apiResult.ProcessingTime;

                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"),
                        e.TriggerReason, apiResult.ProcessingTime));
                }
            }
            
            // 触发翻译完成事件
            OnStatusUpdated(currentUiStatus);
            OnTranslationCompleted(resultArgs);

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
                    message.Split('\n')[0]));
            }
            catch (Exception ex)
            {
                oscArgs.IsSuccess = false;
                oscArgs.ErrorMessage = ex.Message;
                
                var errorStatus = string.Format(LanguageManager.GetString("StatusOscSendFailed"), 
                    ex.Message.Split('\n')[0]);
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



        /// <summary>
        /// 使用新API结果处理模板
        /// </summary>
        /// <param name="template">模板字符串</param>
        /// <param name="apiResult">新API结果</param>
        /// <returns>处理后的文本，如果包含未替换的占位符则返回空字符串</returns>
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
        /// <param name="apiResult">API结果</param>
        /// <param name="targetLanguageCodes">目标语言代码列表</param>
        /// <returns>格式化的输出文本</returns>
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