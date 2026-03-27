using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace lingualink_client.Models
{
    public class AppSettings
    {
        public const string OfficialProductionServerUrl = "https://api.lingualink.aiatechco.com/api/v1/";
        public const string DefaultAuthServerUrl = "https://auth.lingualink.aiatechco.com";


        /// <summary>
        /// 获取有效的官方 Core 服务器 URL。
        /// 优先使用环境变量 LINGUALINK_CORE_SERVER_URL，未设置则回退到生产默认值。
        /// </summary>
        public static string GetEffectiveOfficialServerUrl()
        {
            var envUrl = Environment.GetEnvironmentVariable("LINGUALINK_CORE_SERVER_URL");
            return string.IsNullOrWhiteSpace(envUrl) ? OfficialProductionServerUrl : envUrl;
        }

        /// <summary>
        /// 获取有效的 Auth 服务器 URL。
        /// 优先使用环境变量 LINGUALINK_AUTH_SERVER_URL，未设置则回退到生产默认值。
        /// </summary>
        public static string GetEffectiveAuthServerUrl()
        {
            var envUrl = Environment.GetEnvironmentVariable("LINGUALINK_AUTH_SERVER_URL");
            return string.IsNullOrWhiteSpace(envUrl) ? DefaultAuthServerUrl : envUrl.TrimEnd('/');
        }

        public string GlobalLanguage { get; set; } = Services.LanguageManager.DetectSystemLanguage();

        public string TargetLanguages { get; set; } = "英文,日文"; // Default: English, Japanese

        // Legacy compatibility only. New code should use ActiveServerUrl instead.
        [Obsolete("Use ActiveServerUrl instead. Kept only for JSON deserialization compatibility.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ServerUrl { get; set; }

        // Legacy compatibility only. New code should use ActiveApiKey instead.
        [Obsolete("Use ActiveApiKey instead. Kept only for JSON deserialization compatibility.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiKey { get; set; }

        // Optional override for update feed (useful for debugging update service)
        public string UpdateFeedOverride { get; set; } = string.Empty;

        // VAD Parameters (defaults from your AudioService)
        public double PostSpeechRecordingDurationSeconds { get; set; } = 0.6; // 追加录音时长，用于捕捉尾音
        public double MinVoiceDurationSeconds { get; set; } = 0.7;
        public double MaxVoiceDurationSeconds { get; set; } = 10.0;
        public double MinRecordingVolumeThreshold { get; set; } = 0.05;

         // OSC Settings for VRChat
        public bool EnableOsc { get; set; } = true;
        public string OscIpAddress { get; set; } = "127.0.0.1";
        public int OscPort { get; set; } = 9000; // VRChat default input port
        public bool OscSendImmediately { get; set; } = true; // Corresponds to 'b' param in /chatbox/input
        public bool OscPlayNotificationSound { get; set; } = false; // Corresponds to 'n' param, false is less intrusive

        // Message Template Settings
        public string MessageTemplate { get; set; } = "{raw_text}"; // Default template
        public bool UseCustomTemplate { get; set; } = false; // Whether to use template system or raw text
        public string UserCustomTemplateText { get; set; } = "{英文}\n{日文}\n{中文}"; // User's custom template text that persists

        // Audio Encoding Settings - Opus is always enabled with fixed 16kbps bitrate
        public int OpusComplexity { get; set; } = 7; // 编码复杂度 (5-10, 7是默认选择，更高压缩率)

        // Audio Enhancement Settings - 音频增强设置
        public bool EnableAudioNormalization { get; set; } = true; // 是否启用峰值归一化
        public double NormalizationTargetDb { get; set; } = -3.0; // 峰值归一化目标电平 (dBFS)
        public bool EnableQuietBoost { get; set; } = true; // 是否启用安静语音增强
        public double QuietBoostRmsThresholdDbFs { get; set; } = -25.0; // RMS阈值，低于此值的片段将被增强 (dBFS)
        public double QuietBoostGainDb { get; set; } = 6.0; // 对安静片段应用的增益量 (dB)

        // Conversation history configuration
        public bool ConversationHistoryEnabled { get; set; } = true;
        public string ConversationHistoryStoragePath { get; set; } = string.Empty;
        public int ConversationHistoryRetentionDays { get; set; } = 90;
        public bool ConversationHistoryIncludeFailures { get; set; } = false;

        // Microphone selection - remember last used device
        public string LastSelectedMicrophoneId { get; set; } = string.Empty;

        // Account / server selection
        public bool UseCustomServer { get; set; } = false;
        public string CustomServerUrl { get; set; } = string.Empty;
        public string CustomApiKey { get; set; } = string.Empty;
        public string OfficialServerUrl { get; set; } = GetEffectiveOfficialServerUrl();
        // 官方模式走 OAuth Bearer token，不需要 API Key，此字段已废弃
        [Obsolete("Official mode uses OAuth Bearer tokens. Kept only for JSON deserialization compatibility.")]
        public string OfficialApiKey { get; set; } = string.Empty;
        public string PendingSubscriptionOrderOutTradeNo { get; set; } = string.Empty;

        /// <summary>
        /// 用户永久关闭的公告 ID 列表，持久化到 app_settings.json。
        /// </summary>
        public List<string> DismissedAnnouncementIds { get; set; } = new();

        [JsonIgnore]
        public string ActiveServerUrl => UseCustomServer
            ? CustomServerUrl
            : (string.IsNullOrWhiteSpace(OfficialServerUrl) ? GetEffectiveOfficialServerUrl() : OfficialServerUrl);

        [JsonIgnore]
        public string ActiveApiKey => UseCustomServer ? CustomApiKey : string.Empty;

        // Get the currently selected template
        public MessageTemplate GetSelectedTemplate()
        {
            if (UseCustomTemplate)
            {
                // Return user's custom template
                return new MessageTemplate("自定义模板", UserCustomTemplateText, "用户自定义模板");
            }
            
            // Default fallback when custom template is disabled
            return new MessageTemplate("完整文本", "{raw_text}", "显示服务器返回的完整原始文本");
        }
    }
}
