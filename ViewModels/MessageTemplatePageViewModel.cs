using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
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

            // Create a sample ApiResult for previewing with the new API format
            var sampleApiResult = new ApiResult
            {
                IsSuccess = true,
                Transcription = "This is the source text.",
                Translations = new Dictionary<string, string>
                {
                    { "en", "Hello World" },
                    { "ja", "こんにちは世界" },
                    { "zh", "你好世界" },
                    { "ko", "안녕하세요 세계" },
                    { "fr", "Bonjour le monde" },
                    { "de", "Hallo Welt" },
                    { "es", "Hola Mundo" },
                    { "ru", "Привет, мир" },
                    { "it", "Ciao mondo" }
                }
            };

            // Use the updated ApiResultProcessor to generate the preview
            TemplatePreview = ApiResultProcessor.ProcessTemplate(CustomTemplateText, sampleApiResult);
        }

        [RelayCommand]
        private void ResetTemplate()
        {
            // Reset to a universal, language-code based template
            CustomTemplateText = "{en}\n{ja}\n{zh}";

            // The SaveSettings() call will be triggered by the change to CustomTemplateText
        }

        [RelayCommand]
        private void InsertPlaceholder(string placeholder)
        {
            if (string.IsNullOrEmpty(placeholder))
                return;

            // Extract the language code from the button's content
            // Format: "English ({en})" -> "{en}"
            var match = System.Text.RegularExpressions.Regex.Match(placeholder, @"\{([a-z]{2,3}(?:-[A-Za-z0-9]+)?)\}");
            if (match.Success)
            {
                CustomTemplateText += match.Value; // Insert {en}, {ja}, etc.
            }
            else
            {
                // Fallback: if it's already in the correct format, use it directly
                CustomTemplateText += placeholder;
            }
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