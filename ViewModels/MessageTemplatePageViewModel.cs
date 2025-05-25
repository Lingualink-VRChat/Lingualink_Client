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

        public List<string> PlaceholderList { get; } = new();

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
            _appSettings.EnsureDefaultTemplates();

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
            // Reset to a simple template showing original and main translations based on current language
            var currentLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            
            if (currentLanguage.StartsWith("zh")) // Chinese
            {
                CustomTemplateText = "{原文}\n{英文}\n{日文}";
            }
            else // English and other languages
            {
                CustomTemplateText = "{原文}\n{英文}\n{日文}";
            }
            
            SaveSettings();
        }

        [RelayCommand]
        private void InsertPlaceholder(string placeholder)
        {
            if (string.IsNullOrEmpty(placeholder))
                return;

            // Extract the actual placeholder from display text (e.g., "{原文} (Original)" -> "{原文}")
            string actualPlaceholder = placeholder;
            if (placeholder.Contains(" (") && placeholder.EndsWith(")"))
            {
                actualPlaceholder = placeholder.Substring(0, placeholder.IndexOf(" ("));
            }

            CustomTemplateText += actualPlaceholder;
        }

        private void SaveSettings()
        {
            // 确保保存当前的界面语言，避免语言切换bug
            _appSettings.GlobalLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            _settingsService.SaveSettings(_appSettings);
            // 通知其他组件设置已更改
            SettingsChangedNotifier.RaiseSettingsChanged();
        }

        public void RefreshSettings()
        {
            LoadSettings();
        }
    }
} 