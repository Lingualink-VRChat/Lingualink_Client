using System;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services.Managers
{
    /// <summary>
    /// 音频翻译协调器 - 负责协调音频处理、翻译和OSC发送的完整流程
    /// </summary>
    public class AudioTranslationOrchestrator : IAudioTranslationOrchestrator, IDisposable
    {
        private readonly AudioService _audioService;
        private readonly TranslationService _translationService;
        private readonly OscService? _oscService;
        private readonly AppSettings _appSettings;
        private readonly ILoggingManager _loggingManager;

        public bool IsWorking => _audioService.IsWorking;

        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<TranslationResultEventArgs>? TranslationCompleted;
        public event EventHandler<OscMessageEventArgs>? OscMessageSent;

        public AudioTranslationOrchestrator(
            AppSettings appSettings,
            ILoggingManager loggingManager)
        {
            _appSettings = appSettings;
            _loggingManager = loggingManager;

            _translationService = new TranslationService(
                _appSettings.ServerUrl,
                _appSettings.ApiKey);
            _audioService = new AudioService(_appSettings);

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

        private async void OnAudioSegmentReadyForTranslation(object? sender, AudioSegmentEventArgs e)
        {
            var currentUiStatus = string.Format(LanguageManager.GetString("StatusSendingSegment"), e.TriggerReason);
            OnStatusUpdated(currentUiStatus);
            _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogSendingSegment"), 
                e.TriggerReason, e.AudioData.Length, DateTime.Now));

            var waveFormat = new WaveFormat(AudioService.APP_SAMPLE_RATE, 16, AudioService.APP_CHANNELS);
            
            // 确定目标语言
            string targetLanguagesForRequest;
            if (_appSettings.UseCustomTemplate)
            {
                // 从模板提取语言
                var templateLanguages = TemplateProcessor.ExtractLanguagesFromTemplate(_appSettings.UserCustomTemplateText);
                targetLanguagesForRequest = string.Join(",", templateLanguages);
            }
            else
            {
                // 使用手动选择的目标语言
                targetLanguagesForRequest = _appSettings.TargetLanguages;
            }
            
            var (response, rawJsonResponse, errorMessage) = await _translationService.TranslateAudioSegmentAsync(
                e.AudioData, waveFormat, e.TriggerReason, targetLanguagesForRequest
            );

            string translatedTextForOsc = string.Empty;
            var resultArgs = new TranslationResultEventArgs
            {
                TriggerReason = e.TriggerReason
            };

            if (!string.IsNullOrEmpty(rawJsonResponse))
            {
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogServerRawResponse"), 
                    e.TriggerReason, rawJsonResponse));
            }

            if (errorMessage != null)
            {
                currentUiStatus = LanguageManager.GetString("StatusTranslationFailed");
                resultArgs.IsSuccess = false;
                resultArgs.ErrorMessage = errorMessage;
                resultArgs.ProcessedText = string.Format(LanguageManager.GetString("TranslationError"), errorMessage);
                
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationError"), 
                    e.TriggerReason, errorMessage));
            }
            else if (response != null)
            {
                if (response.Status == "success" && response.Data != null && !string.IsNullOrEmpty(response.Data.Raw_Text))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccess");
                    resultArgs.IsSuccess = true;
                    resultArgs.OriginalText = response.Data.Raw_Text;
                    resultArgs.DurationSeconds = response.Duration_Seconds;
                    
                    // 使用模板系统生成OSC文本
                    if (_appSettings.UseCustomTemplate)
                    {
                        var selectedTemplate = _appSettings.GetSelectedTemplate();
                        var validatedText = TemplateProcessor.ProcessTemplateWithValidation(selectedTemplate.Template, response.Data);
                        
                        if (validatedText != null)
                        {
                            translatedTextForOsc = validatedText;
                        }
                        else
                        {
                            // Template contains unreplaced placeholders, skip OSC sending
                            translatedTextForOsc = string.Empty;
                            _loggingManager.AddMessage(string.Format("Template processing failed - contains unreplaced placeholders. Skipping OSC send for trigger: {0}", e.TriggerReason));
                        }
                    }
                    else
                    {
                        translatedTextForOsc = response.Data.Raw_Text;
                    }
                    
                    resultArgs.ProcessedText = translatedTextForOsc;
                    
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccess"), 
                        e.TriggerReason, response.Data.Raw_Text, response.Duration_Seconds));
                }
                else if (response.Status == "success" && (response.Data == null || string.IsNullOrEmpty(response.Data.Raw_Text)))
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationSuccessNoText");
                    resultArgs.IsSuccess = true;
                    resultArgs.ProcessedText = LanguageManager.GetString("TranslationSuccessNoTextPlaceholder");
                    resultArgs.DurationSeconds = response.Duration_Seconds;
                    
                    _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogTranslationSuccessNoText"), 
                        e.TriggerReason, response.Duration_Seconds));
                }
                else
                {
                    currentUiStatus = LanguageManager.GetString("StatusTranslationFailedServer");
                    resultArgs.IsSuccess = false;
                    resultArgs.ErrorMessage = response.Message ?? LanguageManager.GetString("UnknownError");
                    resultArgs.ProcessedText = string.Format(LanguageManager.GetString("TranslationServerError"), 
                        response.Message ?? LanguageManager.GetString("UnknownError"));
                    
                    var logEntry = string.Format(LanguageManager.GetString("LogServerError"), 
                        e.TriggerReason, response.Message ?? LanguageManager.GetString("UnknownError"));
                    if (response.Details != null) 
                        logEntry += string.Format(LanguageManager.GetString("LogServerErrorDetails"), 
                            response.Details.Content ?? "N/A");
                    _loggingManager.AddMessage(logEntry);
                }
            }
            else
            {
                currentUiStatus = LanguageManager.GetString("StatusEmptyResponse");
                resultArgs.IsSuccess = false;
                resultArgs.ProcessedText = LanguageManager.GetString("TranslationEmptyResponseError");
                
                _loggingManager.AddMessage(string.Format(LanguageManager.GetString("LogEmptyResponse"), e.TriggerReason));
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

        public void Dispose()
        {
            _audioService.AudioSegmentReady -= OnAudioSegmentReadyForTranslation;
            _audioService.StatusUpdated -= OnAudioServiceStatusUpdate;
            
            _audioService?.Dispose();
            _translationService?.Dispose();
            _oscService?.Dispose();
        }
    }
} 