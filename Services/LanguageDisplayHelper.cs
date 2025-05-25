using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace lingualink_client.Services
{
    /// <summary>
    /// 语言显示助手类，负责在界面显示的语言名称和后端传参的中文语言名称之间进行转换
    /// </summary>
    public static class LanguageDisplayHelper
    {
        /// <summary>
        /// 后端使用的中文语言名称（不变，用于传参和存储）
        /// </summary>
        public static readonly List<string> BackendLanguageNames = new List<string> 
        { 
            "英文", "日文", "法文", "中文", "韩文", "西班牙文", "俄文", "德文", "意大利文" 
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