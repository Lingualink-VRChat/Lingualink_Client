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
                new CultureInfo("zh-CN")
            };
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
