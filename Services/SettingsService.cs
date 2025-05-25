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
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? CreateDefaultSettings(); // Return default if deserialization fails
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Fallback to default settings
            }
            
            // 首次运行，创建默认设置并保存
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

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                // Optionally notify user or log more formally
            }
        }
    }
}