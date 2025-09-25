using lingualink_client.Models;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// Lingualink Core API v2.0 服务接口
    /// 支持音频和文本处理，自动选择合适的API端点
    /// </summary>
    public interface ILingualinkApiService : IDisposable
    {
        /// <summary>
        /// 处理音频数据（自动转录+翻译）
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="waveFormat">音频格式</param>
        /// <param name="targetLanguages">目标语言代码列表（如：["en", "ja"]）</param>
        /// <param name="task">要执行的任务："translate" 或 "transcribe"</param>
        /// <param name="triggerReason">触发原因</param>
        /// <returns>处理结果</returns>
        Task<ApiResult> ProcessAudioAsync(
            byte[] audioData,
            WaveFormat waveFormat,
            IEnumerable<string> targetLanguages,
            string task = "translate",
            string triggerReason = "manual",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 处理文本翻译
        /// </summary>
        /// <param name="text">要翻译的文本</param>
        /// <param name="targetLanguages">目标语言代码列表（如：["en", "ja"]）</param>
        /// <param name="sourceLanguage">源语言代码（可选，系统会自动检测）</param>
        /// <returns>处理结果</returns>
        Task<ApiResult> ProcessTextAsync(
            string text, 
            IEnumerable<string> targetLanguages,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证API连接和认证
        /// </summary>
        /// <returns>验证结果</returns>
        Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取系统支持的功能和语言
        /// </summary>
        /// <returns>系统能力信息</returns>
        Task<SystemCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取支持的语言列表
        /// </summary>
        /// <returns>语言列表</returns>
        Task<LanguageInfo[]?> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// API处理结果
    /// </summary>
    public class ApiResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RequestId { get; set; }
        public string? Transcription { get; set; }
        public Dictionary<string, string> Translations { get; set; } = new();
        public string? RawResponse { get; set; }
        public double ProcessingTime { get; set; }
        public ApiMetadata? Metadata { get; set; }

        /// <summary>
        /// 转换为旧版ServerResponse格式以保持兼容性
        /// </summary>
        public ServerResponse ToLegacyResponse()
        {
            var translationData = new TranslationData();

            if (!string.IsNullOrEmpty(Transcription))
            {
                translationData.Raw_Text = RawResponse ?? Transcription;
                translationData.原文 = Transcription;
            }

            // 将语言代码转换为中文名称并设置字段
            foreach (var translation in Translations)
            {
                var chineseName = LanguageDisplayHelper.ConvertLanguageCodeToChineseName(translation.Key);
                translationData.SetLanguageField(chineseName, translation.Value);

                // 设置预定义字段以保持向后兼容
                switch (chineseName)
                {
                    case "英文": translationData.英文 = translation.Value; break;
                    case "日文": translationData.日文 = translation.Value; break;
                    case "中文": translationData.中文 = translation.Value; break;
                    case "韩文": translationData.韩文 = translation.Value; break;
                    case "法文": translationData.法文 = translation.Value; break;
                    case "德文": translationData.德文 = translation.Value; break;
                    case "西班牙文": translationData.西班牙文 = translation.Value; break;
                    case "俄文": translationData.俄文 = translation.Value; break;
                    case "意大利文": translationData.意大利文 = translation.Value; break;
                }
            }

            return new ServerResponse
            {
                Status = IsSuccess ? "success" : "error",
                Duration_Seconds = ProcessingTime,
                Data = translationData,
                Message = ErrorMessage
            };
        }
    }

    /// <summary>
    /// 系统能力信息
    /// </summary>
    public class SystemCapabilities
    {
        public AudioProcessingCapabilities? AudioProcessing { get; set; }
        public TextProcessingCapabilities? TextProcessing { get; set; }
        public string[] SupportedLanguages { get; set; } = Array.Empty<string>();
    }

    public class AudioProcessingCapabilities
    {
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
        public long MaxAudioSize { get; set; }
        public string[] SupportedTasks { get; set; } = Array.Empty<string>();
        public bool AudioConversion { get; set; }
    }

    public class TextProcessingCapabilities
    {
        public int MaxTextLength { get; set; }
        public string[] SupportedTasks { get; set; } = Array.Empty<string>();
        public string[] Features { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 语言信息 (已修复以匹配API响应)
    /// </summary>
    public class LanguageInfo
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("display")]
        public string Display { get; set; } = string.Empty; // e.g., "英文"

        [JsonPropertyName("english")]
        public string English { get; set; } = string.Empty; // e.g., "English"

        [JsonPropertyName("aliases")]
        public string[] Aliases { get; set; } = Array.Empty<string>();
    }
}

