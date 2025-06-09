using lingualink_client.Services;
using lingualink_client.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace lingualink_client.ViewModels
{
    public partial class MessageTemplatePageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

        public string MessageTemplateSettings => LanguageManager.GetString("MessageTemplateSettings");
        public string UseCustomTemplate => LanguageManager.GetString("UseCustomTemplate");
        public string CustomTemplateTextLabel => LanguageManager.GetString("CustomTemplateText");
        public string AvailablePlaceholders => LanguageManager.GetString("AvailablePlaceholders");
        public string PreviewTemplate => LanguageManager.GetString("PreviewTemplate");
        public string ResetToDefaults => LanguageManager.GetString("ResetToDefaults");

        [ObservableProperty] private bool _useCustomTemplateEnabled;
        [ObservableProperty] private string _customTemplateText = "";
        [ObservableProperty] private string _templatePreview = "";

        public ObservableCollection<string> PlaceholderList { get; } = new();

        public MessageTemplatePageViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();
            
            LanguageManager.LanguageChanged += () => {
                OnPropertyChanged(nameof(MessageTemplateSettings));
                OnPropertyChanged(nameof(UseCustomTemplate));
                OnPropertyChanged(nameof(CustomTemplateTextLabel));
                OnPropertyChanged(nameof(AvailablePlaceholders));
                OnPropertyChanged(nameof(PreviewTemplate));
                OnPropertyChanged(nameof(ResetToDefaults));
                
                // Refresh placeholders when language changes
                InitializePlaceholders();
            };

            LoadSettings();
            InitializePlaceholders();
        }

        private void LoadSettings()
        {
            _appSettings = _settingsService.LoadSettings();

            UseCustomTemplateEnabled = _appSettings.UseCustomTemplate;
            
            // Load the user's custom template text, preserving their previous work
            CustomTemplateText = _appSettings.UserCustomTemplateText;
            
            UpdatePreview();
        }

        private void InitializePlaceholders()
        {
            PlaceholderList.Clear();
            var placeholders = TemplateProcessor.GetAvailablePlaceholders();
            foreach (var placeholder in placeholders)
            {
                PlaceholderList.Add(placeholder);
            }
        }

        partial void OnUseCustomTemplateEnabledChanged(bool value)
        {
            _appSettings.UseCustomTemplate = value;
            SaveSettings();
        }

        partial void OnCustomTemplateTextChanged(string value)
        {
            // Save the user's custom template text immediately
            _appSettings.UserCustomTemplateText = value;
            SaveSettings();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(CustomTemplateText))
            {
                TemplatePreview = "";
                return;
            }

            // Use the enhanced sample data with multiple languages
            var sampleData = TemplateProcessor.CreateSamplePreviewData();
            TemplatePreview = TemplateProcessor.ProcessTemplate(CustomTemplateText, sampleData);
        }

        [RelayCommand]
        private void ResetTemplate()
        {
            // Reset to a simple template showing main translations based on current language
            var currentLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            
            if (currentLanguage.StartsWith("zh")) // Chinese
            {
                CustomTemplateText = "{英文}\n{日文}\n{中文}";
            }
            else // English and other languages
            {
                CustomTemplateText = "{英文}\n{日文}\n{中文}";
            }
            
            SaveSettings();
        }

        [RelayCommand]
        private void InsertPlaceholder(string placeholder)
        {
            if (string.IsNullOrEmpty(placeholder))
                return;

            string actualPlaceholder = placeholder;
            
            // Extract the actual Chinese placeholder from display text
            // For English mode: "English ({英文})" -> "{英文}"
            // For Chinese mode: "{英文}" -> "{英文}"
            if (placeholder.Contains("({") && placeholder.Contains("})"))
            {
                var startIndex = placeholder.IndexOf("({");
                var endIndex = placeholder.IndexOf("})", startIndex);
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    actualPlaceholder = placeholder.Substring(startIndex + 1, endIndex - startIndex);
                }
            }
            else if (placeholder.Contains(" (") && placeholder.EndsWith(")"))
            {
                // Legacy format support (shouldn't be used anymore, but just in case)
                actualPlaceholder = placeholder.Substring(0, placeholder.IndexOf(" ("));
            }

            CustomTemplateText += actualPlaceholder;
        }

        private void SaveSettings()
        {
            // 重新加载最新设置作为基础，确保不会覆盖其他页面的更改
            AppSettings settingsToSave = _settingsService.LoadSettings();

            // 应用此页面管理的特定设置
            settingsToSave.UseCustomTemplate = this.UseCustomTemplateEnabled;
            settingsToSave.UserCustomTemplateText = this.CustomTemplateText;

            // 确保保存当前的界面语言，避免语言切换bug
            settingsToSave.GlobalLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;

            _settingsService.SaveSettings(settingsToSave);
            _appSettings = settingsToSave; // 更新ViewModel的缓存设置

            // 通过事件聚合器通知设置变更
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Publish(new ViewModels.Events.SettingsChangedEvent
            {
                Settings = settingsToSave,
                ChangeSource = "MessageTemplatePage"
            });
        }

        public void RefreshSettings()
        {
            LoadSettings();
        }
    }
} 