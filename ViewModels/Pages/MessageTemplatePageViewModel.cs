using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Models;
using lingualink_client.Services.Events;
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

        // 新增：用于控制警告提示的属性
        [ObservableProperty] private bool _isTemplateOverLimit = false;
        [ObservableProperty] private string _validationMessage = string.Empty;
        [ObservableProperty] private string _validationTitle = string.Empty;

        public ObservableCollection<PlaceholderItem> PlaceholderList { get; } = new();

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

            // 订阅语言初始化事件
            var eventAggregator = ServiceContainer.Resolve<IEventAggregator>();
            eventAggregator.Subscribe<LanguagesInitializedEvent>(OnLanguagesInitialized);

            LoadSettings();
            InitializePlaceholders();
            ValidateTemplate(); // 初始加载时也进行验证
        }

        private void OnLanguagesInitialized(LanguagesInitializedEvent obj)
        {
            // 当语言加载完成后，刷新占位符列表
            InitializePlaceholders();
        }

        private void LoadSettings()
        {
            _appSettings = _settingsService.LoadSettings();

            UseCustomTemplateEnabled = _appSettings.UseCustomTemplate;

            // Load the user's custom template text, preserving their previous work
            CustomTemplateText = _appSettings.UserCustomTemplateText;

            UpdatePreview();
            ValidateTemplate(); // 加载设置后验证
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
            ValidateTemplate(); // 文本变化时验证
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
        private void InsertPlaceholder(string? placeholderValue)
        {
            if (string.IsNullOrEmpty(placeholderValue))
                return;

            // The command parameter is now the direct value to insert, e.g., "{transcription}" or "{en}"
            CustomTemplateText += placeholderValue;
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
            eventAggregator.Publish(new Services.Events.SettingsChangedEvent
            {
                Settings = settingsToSave,
                ChangeSource = "MessageTemplatePage"
            });
        }

        private void ValidateTemplate()
        {
            // 使用我们新的无限制提取方法来获取所有占位符
            var allPlaceholders = TemplateProcessor.ExtractLanguagesFromTemplate(CustomTemplateText, 0);

            if (allPlaceholders.Count > 3)
            {
                IsTemplateOverLimit = true;
                ValidationTitle = LanguageManager.GetString("TemplateLimitWarningTitle");
                ValidationMessage = LanguageManager.GetString("TemplateLimitWarningMessage");
            }
            else
            {
                IsTemplateOverLimit = false;
            }
        }

        public void RefreshSettings()
        {
            LoadSettings();
        }
    }
}
