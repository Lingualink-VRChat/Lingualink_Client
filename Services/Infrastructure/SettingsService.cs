using System;
using System.IO;
using System.Text.Json;
using lingualink_client.Models;

namespace lingualink_client.Services
{
    public class SettingsService
    {
        private static readonly string SettingsFileName = "app_settings.json";
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            // Store settings in a user-specific application data folder
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appSpecificFolder = Path.Combine(appDataFolder, "lingualink_client");
            Directory.CreateDirectory(appSpecificFolder); // Ensure directory exists
            _settingsFilePath = Path.Combine(appSpecificFolder, SettingsFileName);
        }

        public AppSettings LoadSettings()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Loading settings from: {_settingsFilePath}");

                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        bool normalized = NormalizeSettings(settings);
                        System.Diagnostics.Debug.WriteLine($"[SettingsService] Loaded settings - ServerUrl: '{settings.ServerUrl}'");
                        if (normalized)
                        {
                            SaveSettings(settings);
                        }
                        return settings;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to deserialize settings, using defaults");
                        return CreateDefaultSettings();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] Settings file does not exist: {_settingsFilePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Fallback to default settings
            }

            // 首次运行，创建默认设置并保存
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Creating default settings");
            var defaultSettings = CreateDefaultSettings();
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        private AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings();
            // 确保使用系统语言检测
            settings.GlobalLanguage = LanguageManager.DetectSystemLanguage();
            return settings;
        }

        private static bool NormalizeSettings(AppSettings settings)
        {
            bool changed = false;

            if (string.IsNullOrWhiteSpace(settings.OfficialServerUrl))
            {
                settings.OfficialServerUrl = AppSettings.OfficialProductionServerUrl;
                changed = true;
            }

            bool isLegacyDefaultSelection =
                settings.UseCustomServer
                && string.Equals(settings.ServerUrl, AppSettings.LegacyCustomServerUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(settings.CustomServerUrl, AppSettings.LegacyCustomServerUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(settings.OfficialServerUrl, AppSettings.LegacyLocalOfficialServerUrl, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(settings.ApiKey)
                && string.IsNullOrWhiteSpace(settings.CustomApiKey)
                && string.IsNullOrWhiteSpace(settings.OfficialApiKey);

            if (isLegacyDefaultSelection)
            {
                settings.UseCustomServer = false;
                settings.OfficialServerUrl = AppSettings.OfficialProductionServerUrl;
                settings.ServerUrl = AppSettings.OfficialProductionServerUrl;
                changed = true;
            }

            if (!settings.UseCustomServer
                && string.Equals(settings.ServerUrl, AppSettings.LegacyLocalOfficialServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                settings.ServerUrl = settings.OfficialServerUrl;
                changed = true;
            }

            var officialUrl = NormalizeUrl(settings.OfficialServerUrl);
            var currentUrl = NormalizeUrl(settings.ServerUrl);
            var customUrl = NormalizeUrl(settings.CustomServerUrl);

            // Old desktop builds could persist the official production endpoint inside the
            // "custom server" branch, which disables OAuth-backed Bearer auth for translation
            // requests. When that happens, always migrate back to the official mode
            // automatically so users don't need to delete local settings manually.
            if (!string.IsNullOrWhiteSpace(officialUrl)
                && (string.Equals(currentUrl, officialUrl, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(customUrl, officialUrl, StringComparison.OrdinalIgnoreCase)))
            {
                settings.UseCustomServer = false;
                settings.ServerUrl = settings.OfficialServerUrl;
                settings.CustomServerUrl = settings.OfficialServerUrl;
                settings.ApiKey = string.Empty;
                settings.CustomApiKey = string.Empty;
                settings.OfficialApiKey = string.Empty;
                changed = true;
            }

            return changed;
        }

        private static string NormalizeUrl(string? url)
        {
            return (url ?? string.Empty).Trim().TrimEnd('/');
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Saving settings to: {_settingsFilePath}");
                System.Diagnostics.Debug.WriteLine($"[SettingsService] ServerUrl: '{settings.ServerUrl}'");

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"[SettingsService] Settings saved successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                // Optionally notify user or log more formally
            }
        }
    }
}
