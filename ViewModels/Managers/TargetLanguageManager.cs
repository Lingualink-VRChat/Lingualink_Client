using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.ViewModels.Events;

namespace lingualink_client.ViewModels.Managers
{
    /// <summary>
    /// 目标语言管理器实现 - 管理目标语言的选择和配置
    /// </summary>
    public class TargetLanguageManager : ITargetLanguageManager, INotifyPropertyChanged
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggingManager _logger;
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings = null!;
        private bool _isLoadingSettings = false;

        public ObservableCollection<SelectableTargetLanguageViewModel> LanguageItems { get; }
        
        private bool _areLanguagesEnabled = true;
        public bool AreLanguagesEnabled 
        { 
            get => _areLanguagesEnabled;
            set
            {
                if (_areLanguagesEnabled != value)
                {
                    _areLanguagesEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AreLanguagesEnabled)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddLanguage))); // Notify CanAddLanguage
                    Debug.WriteLine($"TargetLanguageManager: AreLanguagesEnabled changed to {value}");
                }
            }
        }
        
        public int MaxLanguageCount { get; } = 3; // VRChat OSC限制，最多3种语言以确保更好的性能
        
        public int CurrentLanguageCount => LanguageItems.Count;
        
        public bool CanAddLanguage => CurrentLanguageCount < MaxLanguageCount && AreLanguagesEnabled;

        public event EventHandler? LanguagesChanged;
        public event EventHandler<bool>? EnabledStateChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        private static readonly string[] AllSupportedLanguages = LanguageDisplayHelper.BackendLanguageNames.ToArray();

        public TargetLanguageManager()
        {
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();
            _logger = ServiceContainer.Resolve<ILoggingManager>();
            _settingsService = new SettingsService();
            
            LanguageItems = new ObservableCollection<SelectableTargetLanguageViewModel>();
            
            // Load initial AppSettings to have a reference
            // This will be overwritten by LoadFromSettings if called later with a different instance
            _appSettings = _settingsService.LoadSettings();
            
            Debug.WriteLine("TargetLanguageManager: Initialized");
        }

        public void LoadFromSettings(AppSettings settings)
        {
            _isLoadingSettings = true;
            _appSettings = settings; // Crucial: Store the AppSettings instance being used by the system
            
            try
            {
                Debug.WriteLine("TargetLanguageManager: Loading from settings");
                
                LanguageItems.Clear();
                
                var languagesFromSettings = string.IsNullOrWhiteSpace(settings.TargetLanguages)
                    ? new string[0]
                    : settings.TargetLanguages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => s.Trim())
                                             // Ensure we only load languages that are known backend names
                                             .Where(s => AllSupportedLanguages.Contains(s))
                                             .Distinct()
                                             .ToArray();

                if (!languagesFromSettings.Any() && !string.IsNullOrWhiteSpace(settings.TargetLanguages))
                {
                    // This case means settings.TargetLanguages had values, but none were valid backend names.
                    // Default to the first backend language.
                    _logger.AddMessage($"Warning: TargetLanguages in settings ('{settings.TargetLanguages}') were invalid. Defaulting.");
                    languagesFromSettings = new[] { AllSupportedLanguages.FirstOrDefault() ?? "英文" };
                }
                else if (!languagesFromSettings.Any())
                {
                    // This case means settings.TargetLanguages was empty or whitespace.
                    languagesFromSettings = new[] { AllSupportedLanguages.FirstOrDefault() ?? "英文" };
                }

                foreach (var lang in languagesFromSettings.Take(MaxLanguageCount))
                {
                    var newItem = new SelectableTargetLanguageViewModel(this, lang, AllSupportedLanguages.ToList());
                    LanguageItems.Add(newItem);
                }
                
                UpdateItemPropertiesAndAvailableLanguagesInternal();
                UpdateEnabledState(settings.UseCustomTemplate); // Also update enabled state based on loaded settings
                
                // Notify that properties might have changed after loading
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddLanguage)));
                
                _logger.AddMessage($"Target languages loaded: {string.Join(", ", LanguageItems.Select(li => li.SelectedLanguage))}");
                Debug.WriteLine($"TargetLanguageManager: Loaded {LanguageItems.Count} languages");
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        // New method to handle saving
        private void UpdateAndPersistTargetLanguages()
        {
            if (_isLoadingSettings || _appSettings == null) return;

            var selectedLangsList = LanguageItems
                .Select(item => item.SelectedLanguage)
                .Where(lang => !string.IsNullOrWhiteSpace(lang) && AllSupportedLanguages.Contains(lang))
                .Distinct()
                .ToList();
            
            _appSettings.TargetLanguages = string.Join(",", selectedLangsList);
            
            _settingsService.SaveSettings(_appSettings);

            // 通过事件聚合器通知设置变更
            _eventAggregator.Publish(new SettingsChangedEvent
            {
                Settings = _appSettings,
                ChangeSource = "TargetLanguageManager"
            });

            _logger.AddMessage(Services.LanguageManager.GetString("LogTargetLangsSaved"));
        }

        public void AddLanguage()
        {
            if (!CanAddLanguage)
            {
                Debug.WriteLine("TargetLanguageManager: Cannot add language - limit reached or disabled");
                return;
            }

            string defaultNewLang = AllSupportedLanguages.FirstOrDefault(l => !LanguageItems.Any(item => item.SelectedLanguage == l))
                                    ?? AllSupportedLanguages.First();
            
            var newItem = new SelectableTargetLanguageViewModel(this, defaultNewLang, AllSupportedLanguages.ToList());
            LanguageItems.Add(newItem);
            
            UpdateItemPropertiesAndAvailableLanguagesInternal();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddLanguage)));
            
            NotifyLanguagesChanged(); // Notifies listeners like TargetLanguageViewModel
            if (!_appSettings.UseCustomTemplate) // Only persist if not in template mode
            {
                UpdateAndPersistTargetLanguages();
            }
            
            _logger.AddMessage($"Added target language: {defaultNewLang}");
            Debug.WriteLine($"TargetLanguageManager: Added language {defaultNewLang}. Total: {LanguageItems.Count}");
        }

        public void RemoveLanguage(SelectableTargetLanguageViewModel item)
        {
            if (item == null || !LanguageItems.Contains(item))
            {
                Debug.WriteLine("TargetLanguageManager: Cannot remove language - item not found");
                return;
            }

            if (LanguageItems.Count <= 1)
            {
                Debug.WriteLine("TargetLanguageManager: Cannot remove language - must have at least one");
                return;
            }

            var languageName = item.SelectedLanguage;
            LanguageItems.Remove(item);
            
            UpdateItemPropertiesAndAvailableLanguagesInternal();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAddLanguage)));

            NotifyLanguagesChanged();
            if (!_appSettings.UseCustomTemplate) // Only persist if not in template mode
            {
                UpdateAndPersistTargetLanguages();
            }
            
            _logger.AddMessage($"Removed target language: {languageName}");
            Debug.WriteLine($"TargetLanguageManager: Removed language {languageName}. Total: {LanguageItems.Count}");
        }

        public string GetTargetLanguagesForRequest(AppSettings settings)
        {
            if (settings.UseCustomTemplate)
            {
                // 从模板提取语言
                var templateLanguages = TemplateProcessor.ExtractLanguagesFromTemplate(settings.UserCustomTemplateText);
                var result = string.Join(",", templateLanguages);
                Debug.WriteLine($"TargetLanguageManager: Template mode - extracted languages: {result}");
                return result;
            }
            else
            {
                // 使用手动选择的语言
                var selectedLanguages = LanguageItems
                    .Select(item => item.SelectedLanguage)
                    .Where(lang => !string.IsNullOrWhiteSpace(lang) && AllSupportedLanguages.Contains(lang))
                    .Distinct()
                    .ToArray();
                
                var result = string.Join(",", selectedLanguages);
                Debug.WriteLine($"TargetLanguageManager: Manual mode - selected languages: {result}");
                return result;
            }
        }

        public void UpdateEnabledState(bool useCustomTemplate)
        {
            var newState = !useCustomTemplate;
            if (AreLanguagesEnabled != newState)
            {
                AreLanguagesEnabled = newState;
                EnabledStateChanged?.Invoke(this, AreLanguagesEnabled);
                
                Debug.WriteLine($"TargetLanguageManager: Enabled state changed to {AreLanguagesEnabled}");
                
                // 发布事件
                _eventAggregator.Publish(new TargetLanguagesChangedEvent
                {
                    Languages = GetCurrentLanguages(),
                    IsTemplateMode = useCustomTemplate,
                    Source = "EnabledStateChanged"
                });
            }
        }

        /// <summary>
        /// 当语言选择发生变化时调用（由SelectableTargetLanguageViewModel调用）
        /// </summary>
        public void OnLanguageSelectionChanged(SelectableTargetLanguageViewModel changedItem)
        {
            UpdateItemPropertiesAndAvailableLanguagesInternal();
            
            if (!_isLoadingSettings)
            {
                NotifyLanguagesChanged();
                if (!_appSettings.UseCustomTemplate) // Only persist if not in template mode
                {
                    UpdateAndPersistTargetLanguages();
                }
            }
        }

        /// <summary>
        /// 更新语言项属性和可用语言（公开方法）
        /// </summary>
        public void UpdateItemPropertiesAndAvailableLanguages()
        {
            UpdateItemPropertiesAndAvailableLanguagesInternal();
        }

        private void UpdateItemPropertiesAndAvailableLanguagesInternal()
        {
            for (int i = 0; i < LanguageItems.Count; i++)
            {
                var itemVm = LanguageItems[i];
                
                // 使用本地化的目标标签
                itemVm.Label = $"{LanguageManager.GetString("TargetLabel")} {i + 1}:";
                itemVm.CanRemove = LanguageItems.Count > 1;
                
                // 构建这个下拉框可用的语言选项（排除其他下拉框已选中的选项）
                var availableBackendLanguages = AllSupportedLanguages
                    .Where(langOption => langOption == itemVm.SelectedLanguage || 
                                       !LanguageItems.Where(it => it != itemVm).Any(it => it.SelectedLanguage == langOption))
                    .ToList();
                
                // 确保当前选中的语言在列表中
                if (!string.IsNullOrEmpty(itemVm.SelectedLanguage) && !availableBackendLanguages.Contains(itemVm.SelectedLanguage))
                {
                    availableBackendLanguages.Add(itemVm.SelectedLanguage);
                }
                
                // 更新可用语言列表
                itemVm.UpdateAvailableLanguages(availableBackendLanguages);
            }
        }

        private void NotifyLanguagesChanged()
        {
            LanguagesChanged?.Invoke(this, EventArgs.Empty);
            
            // 发布事件
            _eventAggregator.Publish(new TargetLanguagesChangedEvent
            {
                Languages = GetCurrentLanguages(),
                IsTemplateMode = false, // 手动模式
                Source = "LanguageSelectionChanged"
            });
        }

        private string[] GetCurrentLanguages()
        {
            return LanguageItems
                .Select(item => item.SelectedLanguage)
                .Where(lang => !string.IsNullOrWhiteSpace(lang))
                .ToArray();
        }
    }
} 