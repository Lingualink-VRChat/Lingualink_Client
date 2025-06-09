using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services
{
    /// <summary>
    /// 语言显示助手类，负责在界面显示的语言名称、后端传参的中文语言名称和新API语言代码之间进行转换
    /// 支持从API动态加载语言列表，并提供硬编码回退机制
    /// </summary>
    public static class LanguageDisplayHelper
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // 将硬编码列表替换为可动态填充的属性
        public static List<string> BackendLanguageNames { get; private set; } = new List<string>();
        private static Dictionary<string, string> ChineseToLanguageCode { get; set; } = new Dictionary<string, string>();
        private static Dictionary<string, string> LanguageCodeToChinese { get; set; } = new Dictionary<string, string>();
        private static Dictionary<string, string> ChineseToEnglish { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 从服务器异步初始化语言列表。这是新的入口点。
        /// </summary>
        public static async Task InitializeAsync(ILingualinkApiService apiService)
        {
            lock (_lock)
            {
                if (_isInitialized) return;
            }

            Debug.WriteLine("[LanguageDisplayHelper] Initializing languages from API...");
            try
            {
                var languages = await apiService.GetSupportedLanguagesAsync();
                if (languages != null && languages.Any())
                {
                    var backendNames = new List<string>();
                    var chineseToCode = new Dictionary<string, string>();
                    var codeToChinese = new Dictionary<string, string>();
                    var chineseToEnglish = new Dictionary<string, string>();

                    foreach (var lang in languages)
                    {
                        // [核心修复] 确保直接从 lang 对象访问属性，而不是 lang.Names
                        // API的'display' name 是中文名, e.g., "英文", "繁體中文"
                        var backendName = lang.Display; // 使用 'display' 字段作为内部标识符
                        if (string.IsNullOrWhiteSpace(backendName))
                        {
                            Debug.WriteLine($"[LanguageDisplayHelper] Warning: Received language with empty display name. Code: {lang.Code}. Skipping.");
                            continue;
                        }

                        backendNames.Add(backendName);
                        chineseToCode[backendName] = lang.Code;
                        codeToChinese[lang.Code] = backendName;
                        chineseToEnglish[backendName] = lang.English; // 使用 lang.English

                        Debug.WriteLine($"[LanguageDisplayHelper] Added language: {backendName} -> {lang.Code} ({lang.English})");
                    }

                    // 原子性地更新静态属性
                    BackendLanguageNames = backendNames;
                    ChineseToLanguageCode = chineseToCode;
                    LanguageCodeToChinese = codeToChinese;
                    ChineseToEnglish = chineseToEnglish;

                    Debug.WriteLine($"[LanguageDisplayHelper] Successfully initialized {BackendLanguageNames.Count} languages from API.");
                }
                else
                {
                    Debug.WriteLine("[LanguageDisplayHelper] API returned no languages. Using hardcoded fallbacks.");
                    LoadFallbackLanguages();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LanguageDisplayHelper] Error initializing languages: {ex.Message}. Using hardcoded fallbacks.");
                LoadFallbackLanguages();
            }
            finally
            {
                lock (_lock)
                {
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// 当API调用失败时，加载硬编码的备用语言列表。
        /// </summary>
        private static void LoadFallbackLanguages()
        {
            // [核心修复] 更新这里的中文名称以匹配新的API display名称
            BackendLanguageNames = new List<string> { "英文", "日文", "法语", "中文", "韩文", "西班牙语", "俄语", "德语", "意大利语" };
            ChineseToLanguageCode = new Dictionary<string, string>
            {
                { "英文", "en" }, { "日文", "ja" }, { "法语", "fr" }, { "中文", "zh" }, { "韩文", "ko" },
                { "西班牙语", "es" }, { "俄语", "ru" }, { "德语", "de" }, { "意大利语", "it" }
            };
            LanguageCodeToChinese = ChineseToLanguageCode.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            // 添加繁体中文映射
            LanguageCodeToChinese["zh-hant"] = "繁體中文"; // 确保与API响应一致
            ChineseToEnglish = new Dictionary<string, string>
            {
                 { "英文", "English" }, { "日文", "Japanese" }, { "法语", "French" }, { "中文", "Chinese" }, { "韩文", "Korean" },
                 { "西班牙语", "Spanish" }, { "俄语", "Russian" }, { "德语", "German" }, { "意大利语", "Italian" },
                 { "繁體中文", "Traditional Chinese" } // 确保包含所有可能的显示名称
            };
        }

        /// <summary>
        /// 获取语言的显示名称（根据当前界面语言进行本地化）
        /// </summary>
        /// <param name="backendLanguageName">后端语言名称（中文或繁体中文）</param>
        /// <returns>本地化的显示名称</returns>
        public static string GetDisplayName(string backendLanguageName)
        {
            if (string.IsNullOrEmpty(backendLanguageName)) return backendLanguageName;

            // [核心修复] 对资源键进行规范化，移除空格和特殊字符
            // 例如 "繁體中文" -> "Lang_繁體中文"
            string resourceKey = $"Lang_{backendLanguageName.Replace(" ", "")}";
            string localizedName = LanguageManager.GetString(resourceKey);

            // 如果资源文件没有对应的本地化字符串，则使用英文名作为回退
            // `localizedName == resourceKey` 检查确保了我们能识别出未找到资源的情况
            if (localizedName == resourceKey || string.IsNullOrEmpty(localizedName))
            {
                // 如果本地化查找失败，则从 `ChineseToEnglish` 字典中查找英文名
                return ChineseToEnglish.TryGetValue(backendLanguageName, out var englishName) ? englishName : backendLanguageName;
            }
            return localizedName;
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
            if (string.IsNullOrEmpty(languageCode)) return string.Empty;
            // 增加对繁体中文代码的回退查找
            if (languageCode.Equals("zh-hant", StringComparison.OrdinalIgnoreCase) && !LanguageCodeToChinese.ContainsKey(languageCode))
            {
                return LanguageCodeToChinese.TryGetValue("zh", out var fallbackName) ? fallbackName : languageCode;
            }
            return LanguageCodeToChinese.TryGetValue(languageCode, out var chineseName) ? chineseName : languageCode;
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