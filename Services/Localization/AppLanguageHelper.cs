using System.Threading;
using lingualink_client.Models;

namespace lingualink_client.Services
{
    /// <summary>
    /// Helper for coordinating AppSettings.GlobalLanguage with the current UI language.
    /// Keeps language application and persistence logic in one place so that
    /// pages and view models don't have to duplicate these details.
    /// </summary>
    public static class AppLanguageHelper
    {
        /// <summary>
        /// Apply the language stored in AppSettings to the application by
        /// delegating to LanguageManager.ChangeLanguage.
        /// </summary>
        public static void ApplyLanguage(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            LanguageManager.ChangeLanguage(settings.GlobalLanguage);
        }

        /// <summary>
        /// Capture the current UI culture into AppSettings.GlobalLanguage so that
        /// the next startup will restore the same language.
        /// </summary>
        public static void CaptureCurrentLanguage(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.GlobalLanguage = Thread.CurrentThread.CurrentUICulture.Name;
        }
    }
}

