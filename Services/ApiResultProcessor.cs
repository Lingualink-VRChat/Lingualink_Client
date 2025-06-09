using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace lingualink_client.Services
{
    /// <summary>
    /// API结果处理器 - 提供模板处理和目标语言输出生成的通用方法
    /// 从AudioTranslationOrchestrator和TextTranslationOrchestrator中提取的公共逻辑
    /// </summary>
    public static class ApiResultProcessor
    {
        /// <summary>
        /// 使用新API结果处理模板 (已更新为使用语言代码占位符)
        /// </summary>
        /// <param name="template">模板字符串, e.g., "EN: {en}\nJP: {ja}"</param>
        /// <param name="apiResult">新API结果</param>
        /// <returns>处理后的文本</returns>
        public static string ProcessTemplate(string template, ApiResult apiResult)
        {
            if (string.IsNullOrEmpty(template) || apiResult == null)
                return string.Empty;

            string result = template;

            // 替换原文占位符 (支持多种格式)
            if (!string.IsNullOrEmpty(apiResult.Transcription))
            {
                result = result.Replace("{source_text}", apiResult.Transcription);
                result = result.Replace("{transcription}", apiResult.Transcription);
                // 保留旧格式以向后兼容
                result = result.Replace("{原文}", apiResult.Transcription);
                result = result.Replace("{raw_text}", apiResult.Transcription);
                result = result.Replace("{原始文本}", apiResult.Transcription);
                result = result.Replace("{完整文本}", apiResult.RawResponse ?? apiResult.Transcription);
            }

            // 替换翻译占位符 - 直接使用语言代码
            foreach (var translation in apiResult.Translations)
            {
                // translation.Key is "en", "ja", etc.
                // The placeholder is now {en}, {ja}, etc.
                result = result.Replace($"{{{translation.Key}}}", translation.Value);

                // (可选) 为了完全向后兼容，可以保留对中文名称的替换
                var chineseName = LanguageDisplayHelper.ConvertLanguageCodeToChineseName(translation.Key);
                if (!string.IsNullOrEmpty(chineseName))
                {
                    result = result.Replace($"{{{chineseName}}}", translation.Value);
                }
            }

            // [重要改动] 移除严格的占位符检查。
            // 现在，即使有未替换的占位符，我们也会返回部分处理后的结果。
            // 这为用户提供了更好的反馈。
            // if (TemplateProcessor.ContainsUnreplacedPlaceholders(result))
            // {
            //     return string.Empty;
            // }

            return result;
        }

        /// <summary>
        /// 根据API结果和目标语言代码生成输出文本
        /// </summary>
        /// <param name="apiResult">API结果</param>
        /// <param name="targetLanguageCodes">目标语言代码列表</param>
        /// <param name="logger">日志管理器</param>
        /// <returns>格式化的输出文本</returns>
        public static string GenerateTargetLanguageOutput(ApiResult apiResult, List<string> targetLanguageCodes, ILoggingManager logger)
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
                logger.AddMessage($"Warning: No translations found for target languages [{string.Join(", ", targetLanguageCodes)}], skipping OSC send");
                return string.Empty;
            }

            var result = string.Join("\n", outputParts);
            logger.AddMessage($"Generated output for languages: [{string.Join(", ", targetLanguageCodes)}] -> {outputParts.Count} translations found");

            return result;
        }
    }
}
