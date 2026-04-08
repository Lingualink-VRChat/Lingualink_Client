using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                    bool strippedLegacyFields = StripRetiredServerSettings(json, out json);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        bool normalized = NormalizeSettings(settings);
                        System.Diagnostics.Debug.WriteLine($"[SettingsService] Loaded settings - ActiveServerUrl: '{settings.ActiveServerUrl}'");
                        if (normalized || strippedLegacyFields)
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

            if (settings.DismissedAnnouncementIds == null)
            {
                settings.DismissedAnnouncementIds = new System.Collections.Generic.List<string>();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.OfficialServerUrl))
            {
                settings.OfficialServerUrl = AppSettings.GetEffectiveOfficialServerUrl();
                changed = true;
            }

            return changed;
        }

        private static bool StripRetiredServerSettings(string rawJson, out string sanitizedJson)
        {
            sanitizedJson = rawJson;

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return false;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(rawJson);
            }
            catch
            {
                return false;
            }

            if (root is not JsonObject obj)
            {
                return false;
            }

            bool changed = false;
            changed |= obj.Remove("UseCustomServer");
            changed |= obj.Remove("CustomServerUrl");
            changed |= obj.Remove("CustomApiKey");
            changed |= obj.Remove("ServerUrl");
            changed |= obj.Remove("ApiKey");
            changed |= obj.Remove("OfficialApiKey");

            if (!changed)
            {
                return false;
            }

            sanitizedJson = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return true;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Saving settings to: {_settingsFilePath}");
                System.Diagnostics.Debug.WriteLine($"[SettingsService] ActiveServerUrl: '{settings.ActiveServerUrl}'");

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
