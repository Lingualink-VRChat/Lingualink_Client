using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services;
using lingualink_client.ViewModels.Managers;

namespace lingualink_client.ViewModels.Components
{
    /// <summary>
    /// 目标语言ViewModel - 专门负责目标语言的UI逻辑
    /// </summary>
    public partial class TargetLanguageViewModel : ViewModelBase
    {
        private readonly ITargetLanguageManager _languageManager;

        public ObservableCollection<SelectableTargetLanguageViewModel> LanguageItems => _languageManager.LanguageItems;
        public bool AreLanguagesEnabled => _languageManager.AreLanguagesEnabled;

        // 本地化标签
        public string TargetLanguagesLabel => LanguageManager.GetString("TargetLanguages");
        public string AddLanguageLabel => LanguageManager.GetString("AddLanguage");
        public string RemoveLabel => LanguageManager.GetString("Remove");
        public string TemplateActiveHintLabel => LanguageManager.GetString("TemplateActiveHint");

        public TargetLanguageViewModel()
        {
            _languageManager = ServiceContainer.Resolve<ITargetLanguageManager>();

            // 订阅管理器事件
            _languageManager.LanguagesChanged += OnLanguagesChanged;
            _languageManager.PropertyChanged += OnManagerPropertyChanged;

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguagesChanged(object? sender, System.EventArgs e)
        {
            OnPropertyChanged(nameof(LanguageItems));
            AddLanguageCommand.NotifyCanExecuteChanged();
        }

        private void OnManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ITargetLanguageManager.AreLanguagesEnabled):
                    OnPropertyChanged(nameof(AreLanguagesEnabled));
                    break;
                case nameof(ITargetLanguageManager.LanguageItems):
                    OnPropertyChanged(nameof(LanguageItems));
                    break;
                case nameof(ITargetLanguageManager.CanAddLanguage): // Listen for this specifically
                    AddLanguageCommand.NotifyCanExecuteChanged();
                    break;
            }
        }

        private void OnLanguageChanged()
        {
            // 更新所有语言相关的标签
            OnPropertyChanged(nameof(TargetLanguagesLabel));
            OnPropertyChanged(nameof(AddLanguageLabel));
            OnPropertyChanged(nameof(RemoveLabel));
            OnPropertyChanged(nameof(TemplateActiveHintLabel));

            // 更新目标语言项的属性
            _languageManager.UpdateItemPropertiesAndAvailableLanguages();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAddLanguage))]
        private void AddLanguage()
        {
            _languageManager.AddLanguage();
        }

        private bool CanExecuteAddLanguage()
        {
            return _languageManager.CanAddLanguage;
        }

        public void RemoveLanguageItem(SelectableTargetLanguageViewModel itemToRemove)
        {
            _languageManager.RemoveLanguage(itemToRemove);
        }

        public void OnLanguageSelectionChanged(SelectableTargetLanguageViewModel changedItem)
        {
            // 语言选择变更时的处理逻辑已经在管理器中处理
            // 这里可以添加额外的UI相关逻辑
        }

        public void Dispose()
        {
            // 取消订阅事件
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _languageManager.LanguagesChanged -= OnLanguagesChanged;
            _languageManager.PropertyChanged -= OnManagerPropertyChanged;
        }
    }
} 