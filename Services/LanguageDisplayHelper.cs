using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace lingualink_client.Services
{
    /// <summary>
    /// 语言显示助手类，负责在界面显示的语言名称、后端传参的中文语言名称和新API语言代码之间进行转换
    /// </summary>
    public static class LanguageDisplayHelper
    {
        /// <summary>
        /// 后端使用的中文语言名称（不变，用于传参和存储）
        /// 按照新API v2.0支持的语言更新
        /// </summary>
        public static readonly List<string> BackendLanguageNames = new List<string>
        {
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文"
        };

        /// <summary>
        /// 中文语言名称到新API语言代码的映射
        /// </summary>
        private static readonly Dictionary<string, string> ChineseToLanguageCode = new Dictionary<string, string>
        {
            { "英文", "en" },
            { "日文", "ja" },
            { "法文", "fr" },
            { "中文", "zh" },
            { "韩文", "ko" },
            { "西班牙文", "es" },
            { "俄文", "ru" },
            { "德文", "de" },
            { "意大利文", "it" }
        };

        /// <summary>
        /// 新API语言代码到中文语言名称的映射
        /// </summary>
        private static readonly Dictionary<string, string> LanguageCodeToChinese = new Dictionary<string, string>
        {
            { "en", "英文" },
            { "ja", "日文" },
            { "fr", "法文" },
            { "zh", "中文" },
            { "zh-hant", "中文" }, // 繁体中文也映射到中文
            { "ko", "韩文" },
            { "es", "西班牙文" },
            { "ru", "俄文" },
            { "de", "德文" },
            { "it", "意大利文" }
        };

        /// <summary>
        /// 获取语言的显示名称（根据当前界面语言进行本地化）
        /// </summary>
        /// <param name="backendLanguageName">后端语言名称（中文）</param>
        /// <returns>本地化的显示名称</returns>
        public static string GetDisplayName(string backendLanguageName)
        {
            if (string.IsNullOrEmpty(backendLanguageName))
                return backendLanguageName;

            string resourceKey = $"Lang_{backendLanguageName}";
            return LanguageManager.GetString(resourceKey);
        }

        /// <summary>
        /// 获取所有语言的显示名称列表（根据当前界面语言本地化）
        /// </summary>
        /// <returns>本地化的语言显示名称列表</returns>
        public static ObservableCollection<LanguageDisplayItem> GetDisplayLanguages()
        {
            var displayLanguages = new ObservableCollection<LanguageDisplayItem>();
            foreach (var backendName in BackendLanguageNames)
            {
                displayLanguages.Add(new LanguageDisplayItem
                {
                    BackendName = backendName,
                    DisplayName = GetDisplayName(backendName)
                });
            }
            return displayLanguages;
        }

        /// <summary>
        /// 根据显示名称获取后端语言名称
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <returns>后端语言名称（中文）</returns>
        public static string GetBackendName(string displayName)
        {
            foreach (var backendName in BackendLanguageNames)
            {
                if (GetDisplayName(backendName) == displayName)
                {
                    return backendName;
                }
            }
            return displayName; // 如果找不到匹配，返回原值
        }

        /// <summary>
        /// 将中文语言名称转换为新API的语言代码
        /// </summary>
        /// <param name="chineseName">中文语言名称（如"英文"、"日文"）</param>
        /// <returns>语言代码（如"en"、"ja"）</returns>
        public static string ConvertChineseNameToLanguageCode(string chineseName)
        {
            if (string.IsNullOrEmpty(chineseName))
                return string.Empty;

            return ChineseToLanguageCode.TryGetValue(chineseName, out var code) ? code : chineseName;
        }

        /// <summary>
        /// 将新API的语言代码转换为中文语言名称
        /// </summary>
        /// <param name="languageCode">语言代码（如"en"、"ja"）</param>
        /// <returns>中文语言名称（如"英文"、"日文"）</returns>
        public static string ConvertLanguageCodeToChineseName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return string.Empty;

            return LanguageCodeToChinese.TryGetValue(languageCode, out var name) ? name : languageCode;
        }

        /// <summary>
        /// 将中文语言名称列表转换为语言代码列表（用于新API）
        /// </summary>
        /// <param name="chineseNames">中文语言名称列表</param>
        /// <returns>语言代码列表</returns>
        public static List<string> ConvertChineseNamesToLanguageCodes(IEnumerable<string> chineseNames)
        {
            return chineseNames?.Select(ConvertChineseNameToLanguageCode).Where(code => !string.IsNullOrEmpty(code)).ToList() ?? new List<string>();
        }

        /// <summary>
        /// 将语言代码列表转换为中文语言名称列表
        /// </summary>
        /// <param name="languageCodes">语言代码列表</param>
        /// <returns>中文语言名称列表</returns>
        public static List<string> ConvertLanguageCodesToChineseNames(IEnumerable<string> languageCodes)
        {
            return languageCodes?.Select(ConvertLanguageCodeToChineseName).Where(name => !string.IsNullOrEmpty(name)).ToList() ?? new List<string>();
        }

        /// <summary>
        /// 获取所有支持的语言代码
        /// </summary>
        /// <returns>语言代码列表</returns>
        public static List<string> GetAllSupportedLanguageCodes()
        {
            return ChineseToLanguageCode.Values.ToList();
        }

        /// <summary>
        /// 验证语言代码是否受支持
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <returns>是否支持</returns>
        public static bool IsLanguageCodeSupported(string languageCode)
        {
            return LanguageCodeToChinese.ContainsKey(languageCode);
        }

        /// <summary>
        /// 验证中文语言名称是否受支持
        /// </summary>
        /// <param name="chineseName">中文语言名称</param>
        /// <returns>是否支持</returns>
        public static bool IsChineseNameSupported(string chineseName)
        {
            return ChineseToLanguageCode.ContainsKey(chineseName);
        }
    }

    /// <summary>
    /// 语言显示项，包含后端名称和显示名称
    /// </summary>
    public class LanguageDisplayItem
    {
        public string BackendName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayName;
        }
    }
} 