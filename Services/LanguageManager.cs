using System;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows;

namespace lingualink_client.Services
{
    public static class LanguageManager
    {
        public static event Action? LanguageChanged;

        public static List<CultureInfo> GetAvailableLanguages()
        {
            return new List<CultureInfo>
            {
                new CultureInfo("en"),
                new CultureInfo("zh-CN"),
                new CultureInfo("ja")
            };
        }

        /// <summary>
        /// 检测系统语言并返回合适的默认语言
        /// </summary>
        /// <returns>如果系统语言是中文（简体或繁体），返回"zh-CN"；如果是日语，返回"ja"；否则返回"en"</returns>
        public static string DetectSystemLanguage()
        {
            var systemCulture = CultureInfo.CurrentUICulture;

            // 检查是否为中文（简体中文、繁体中文等）
            if (systemCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                systemCulture.Name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-CN";
            }

            // 检查是否为日语
            if (systemCulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
                systemCulture.Name.StartsWith("ja-", StringComparison.OrdinalIgnoreCase))
            {
                return "ja";
            }

            // 其他语言默认使用英文
            return "en";
        }

        public static void ChangeLanguage(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            LanguageChanged?.Invoke();
        }

        public static string GetString(string key)
        {
            return Properties.Lang.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture) ?? key;
        }
    }
}
