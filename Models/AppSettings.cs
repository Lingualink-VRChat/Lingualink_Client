using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace lingualink_client.Models
{
    public class AppSettings
    {
        public const int MaxEnabledCustomVocabularyTables = 1;
        public const int MaxEntriesPerVocabularyTable = 80;
        public const int MaxCustomVocabularyTableCharacters = 2500;
        public const int MaxCustomVocabularyPayloadEntries = MaxEntriesPerVocabularyTable;
        public const int MaxTermCharactersPerVocabularyEntry = 24;
        public const string OfficialProductionServerUrl = AppEndpoints.OfficialApiBaseUrl;
        public const string DefaultAuthServerUrl = AppEndpoints.DefaultAuthServerUrl;

        /// <summary>
        /// 获取有效的官方 Core 服务器 URL。
        /// 优先使用环境变量 LINGUALINK_CORE_SERVER_URL，未设置则回退到生产默认值。
        /// </summary>
        public static string GetEffectiveOfficialServerUrl()
        {
            var envUrl = Environment.GetEnvironmentVariable("LINGUALINK_CORE_SERVER_URL");
            var effectiveUrl = string.IsNullOrWhiteSpace(envUrl) ? OfficialProductionServerUrl : envUrl;
            return AppEndpoints.EnsureTrailingSlash(effectiveUrl);
        }

        /// <summary>
        /// 获取有效的 Auth 服务器 URL。
        /// 优先使用环境变量 LINGUALINK_AUTH_SERVER_URL，未设置则回退到生产默认值。
        /// </summary>
        public static string GetEffectiveAuthServerUrl()
        {
            return AppEndpoints.NormalizeBaseUrl(
                Environment.GetEnvironmentVariable("LINGUALINK_AUTH_SERVER_URL"),
                DefaultAuthServerUrl);
        }

        public string GlobalLanguage { get; set; } = Services.LanguageManager.DetectSystemLanguage();

        public string TargetLanguages { get; set; } = "英文,日文"; // Default: English, Japanese

        public string PeerAudioTargetLanguages { get; set; } = "英文,日文";

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

        // Global hotkey used to toggle start/stop recognition
        public string ToggleRecognitionHotkey { get; set; } = "Ctrl+Alt+F10";

        public string PendingSubscriptionOrderOutTradeNo { get; set; } = string.Empty;

        /// <summary>
        /// 用户永久关闭的公告 ID 列表，持久化到 app_settings.json。
        /// </summary>
        public List<string> DismissedAnnouncementIds { get; set; } = new();

        /// <summary>
        /// 本地自定义词表，支持多张词表、导入导出与独立启停。
        /// </summary>
        public List<CustomVocabularyTable> CustomVocabularyTables { get; set; } = new();

        [JsonIgnore]
        public string ActiveServerUrl => GetEffectiveOfficialServerUrl();

        public static string NormalizeVocabularyTerm(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= MaxTermCharactersPerVocabularyEntry
                ? trimmed
                : trimmed[..MaxTermCharactersPerVocabularyEntry];
        }

        public static List<string> NormalizeVocabularyValues(
            IEnumerable<string>? values,
            int maxCount,
            int maxTotalCharacters)
        {
            if (values == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            var totalCharacters = 0;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var trimmed = value.Trim();
                if (result.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (result.Count >= maxCount)
                {
                    break;
                }

                var remainingCharacters = maxTotalCharacters - totalCharacters;
                if (remainingCharacters <= 0)
                {
                    break;
                }

                var normalized = trimmed.Length <= remainingCharacters
                    ? trimmed
                    : trimmed[..remainingCharacters];

                if (string.IsNullOrWhiteSpace(normalized))
                {
                    break;
                }

                result.Add(normalized);
                totalCharacters += normalized.Length;
            }

            return result;
        }

        public static int CountVocabularyEntryCharacters(
            string term)
        {
            return term?.Length ?? 0;
        }

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

    public class CustomVocabularyTable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "新词表";
        public bool Enabled { get; set; } = true;
        public List<CustomVocabularyEntry> Entries { get; set; } = new();
    }

    public class CustomVocabularyEntry
    {
        public string Term { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
